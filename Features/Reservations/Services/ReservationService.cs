using CheckAccess.Features.Reservations.Models;
using CheckAccess.Infrastructure.Auth;
using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Features.Reservations.Models;
using CheckTrip.Web.Features.Reservations.Models.Operations;
using CheckTrip.Web.Infrastructure.Audit;
using CheckTrip.Web.Infrastructure.Auth;
using CheckTrip.Web.Infrastructure.Repositories;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Features.Reservations.Services;

public class ReservationService
{
    private readonly AppDbContext _db;
    private readonly ReservationRepository _repo;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public ReservationService(
        AppDbContext db,
        ReservationRepository repo,
        ITenantProvider tenant,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _db = db;
        _repo = repo;
        _tenant = tenant;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<ReservationMonitorItem>> GetMonitorAsync()
    {
        var data = await _repo.GetMonitorAsync();

        return data.Select(x => new ReservationMonitorItem
        {
            Id = x.Id,
            ReservationCode = x.ReservationCode,
            Agency = x.Agency?.Name,
            Seller = x.Seller is null ? null : $"{x.Seller.FirstName} {x.Seller.LastName}",
            Status = x.Status,
            PaymentStatus = x.PaymentStatus,
            CreatedAt = x.CreatedAt,
            PassengerCount = x.Items.Count,
            ContactName = x.ContactName,
            ContactPhone = x.ContactPhone
        }).ToList();
    }

    public async Task<AvailabilityResult> GetAvailabilityAsync(Guid routeScheduleId, DateTime date, bool outbound)
    {
        var schedule = await _repo.GetRouteScheduleAsync(routeScheduleId);

        if (schedule is null)
            throw new Exception("Configuración de viaje no encontrada.");

        var reserved = await _repo.CountReservedAsync(routeScheduleId, date, outbound);

        return new AvailabilityResult
        {
            BoatRouteScheduleId = routeScheduleId,
            TravelDate = date.Date,
            Capacity = schedule.Boat.Capacity,
            ExtraCapacity = schedule.Boat.ExtraCapacity,
            Reserved = reserved
        };
    }

    public async Task<List<WeeklyAvailabilityItem>> GetWeeklyAvailabilityAsync(
     DateTime weekStart,
     Guid? routeId)
    {
        var schedules = await _repo.GetRouteSchedulesByRouteAsync(routeId);

        schedules = schedules
            .GroupBy(x => new
            {
                x.RouteId,
                x.BoatId,
                x.ScheduleId
            })
            .Select(x => x.First())
            .ToList();

        var result = new List<WeeklyAvailabilityItem>();

        for (var i = 0; i < 5; i++)
        {
            var date = weekStart.Date.AddDays(i);

            foreach (var schedule in schedules)
            {
                var reserved = await _repo.CountReservedAsync(
                    schedule.Id,
                    date,
                    outbound: true);

                result.Add(new WeeklyAvailabilityItem
                {
                    BoatRouteScheduleId = schedule.Id,
                    BoatId = schedule.BoatId,
                    Boat = schedule.Boat.Name,
                    Route = $"{schedule.Route.Origin} - {schedule.Route.Destination}",
                    Schedule = schedule.Schedule.Name,
                    TravelDate = date,
                    Capacity = schedule.Boat.Capacity,
                    ExtraCapacity = schedule.Boat.ExtraCapacity,
                    Reserved = reserved
                });
            }
        }

        return result;
    }

    public async Task<Guid> CreateAsync(CreateReservationModel model)
    {
        await ValidateAsync(model);

        var tenantId = _tenant.GetTenantId();
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var reservedPassengers = model.Passengers.Count;

        if (model.OutboundRouteScheduleId.HasValue && model.OutboundDate.HasValue)
        {
            var availability = await GetAvailabilityAsync(
                model.OutboundRouteScheduleId.Value,
                model.OutboundDate.Value,
                outbound: true);

            if (availability.Available < reservedPassengers)
                throw new Exception("No existen cupos suficientes para la reserva.");
        }

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            var reservation = new Reservation
            {
                TenantId = tenantId,
                ReservationCode = await GenerateReservationCodeAsync(),
                ExternalReference = model.ExternalReference,
                AgencyId = model.AgencyId,
                SellerId = model.SellerId,
                ContactName = model.ContactName,
                ContactPhone = model.ContactPhone,
                Status = "Active",
                PaymentStatus = model.PaymentStatus,
                CreatedByUserId = user.UserId,
                CreatedAt = DateTime.UtcNow,
                Notes = model.Notes,
                Items = []
            };

            foreach (var passenger in model.Passengers)
            {
                var customer = await GetOrCreateCustomerAsync(passenger, tenantId);

                reservation.Items.Add(new ReservationItem
                {
                    TenantId = tenantId,
                    Reservation = reservation,
                    CustomerId = customer.Id,
                    TripType = GetTripType(passenger),
                    PassengerType = passenger.PassengerType,

                    OutboundRouteScheduleId = passenger.Outbound
                        ? model.OutboundRouteScheduleId
                        : null,

                    OutboundTravelDate = passenger.Outbound && model.OutboundDate.HasValue
                        ? DateOnly.FromDateTime(model.OutboundDate.Value)
                        : null,

                    ReturnRouteScheduleId = passenger.Return
                        ? model.OutboundRouteScheduleId
                        : null,

                    ReturnTravelDate = passenger.Return && passenger.ReturnDate.HasValue
                        ? DateOnly.FromDateTime(passenger.ReturnDate.Value)
                        : null,

                    Status = "Reserved",
                    CreatedAt = DateTime.UtcNow
                });
            }

            _db.Reservations.Add(reservation);

            _db.ReservationHistory.Add(new ReservationHistory
            {
                TenantId = tenantId,
                Reservation = reservation,
                Action = "Create",
                NewStatus = reservation.Status,
                CreatedByUserId = user.UserId,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            await _audit.LogAsync("Reservation", "Create", null, new
            {
                reservation.Id,
                reservation.TenantId,
                reservation.ReservationCode,
                reservation.ExternalReference,
                reservation.AgencyId,
                reservation.SellerId,
                reservation.ContactName,
                reservation.ContactPhone,
                reservation.Status,
                reservation.PaymentStatus,
                reservation.CreatedByUserId,
                reservation.CreatedAt,
                reservation.Notes,
                Items = reservation.Items.Select(x => new
                {
                    x.Id,
                    x.CustomerId,
                    x.PassengerType,
                    x.TripType,
                    x.OutboundRouteScheduleId,
                    x.OutboundTravelDate,
                    x.ReturnRouteScheduleId,
                    x.ReturnTravelDate,
                    x.Status,
                    x.UnitPrice,
                    x.Discount,
                    x.TotalPrice
                }).ToList()
            });

            return reservation.Id;
        });
    }

    public async Task CancelAsync(Guid reservationId, string? reason)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var reservation = await _repo.GetFullAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var oldStatus = reservation.Status;

        reservation.Status = "Cancelled";
        reservation.UpdatedAt = DateTime.UtcNow;

        foreach (var item in reservation.Items)
            item.Status = "Cancelled";

        _db.ReservationHistory.Add(new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "Cancel",
            Reason = reason,
            OldStatus = oldStatus,
            NewStatus = reservation.Status,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Reservation", "Cancel", new { oldStatus }, new
        {
            reservation.Id,
            reservation.ReservationCode,
            reservation.Status,
            reservation.UpdatedAt,
            Items = reservation.Items.Select(x => new
            {
                x.Id,
                x.CustomerId,
                x.Status
            }).ToList()
        });
    }

    public async Task FinishAsync(Guid reservationId)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var reservation = await _repo.GetFullAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var oldStatus = reservation.Status;

        reservation.Status = "Finished";
        reservation.UpdatedAt = DateTime.UtcNow;

        foreach (var item in reservation.Items)
            item.Status = "Finished";

        _db.ReservationHistory.Add(new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "Finish",
            OldStatus = oldStatus,
            NewStatus = reservation.Status,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Reservation", "Finish", new { oldStatus }, new
        {
            reservation.Id,
            reservation.ReservationCode,
            reservation.Status,
            reservation.UpdatedAt,
            Items = reservation.Items.Select(x => new
            {
                x.Id,
                x.CustomerId,
                x.Status
            }).ToList()
        });
    }

    public async Task ChangePaymentStatusAsync(Guid reservationId, string paymentStatus)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var reservation = await _repo.GetFullAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var old = reservation.PaymentStatus;

        reservation.PaymentStatus = paymentStatus;
        reservation.UpdatedAt = DateTime.UtcNow;

        _db.ReservationHistory.Add(new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "ChangePaymentStatus",
            Reason = $"Cambio de pago: {old} -> {paymentStatus}",
            OldStatus = old,
            NewStatus = paymentStatus,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            "Reservation",
            "ChangePaymentStatus",
            new { PaymentStatus = old },
            new { PaymentStatus = paymentStatus });
    }

    public async Task<CustomerLookupModel?> FindCustomerAsync(string documentNumber)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
            return null;

        var customer = await _repo.GetCustomerByDocumentAsync(documentNumber.Trim());

        if (customer is null)
            return null;

        return new CustomerLookupModel
        {
            DocumentType = customer.DocumentType,
            DocumentNumber = customer.DocumentNumber,
            FullName = customer.FullName,
            Nationality = customer.Nationality ?? "",
            Age = customer.Age
        };
    }

    public async Task<List<CustomerLookupModel>> SearchCustomersAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var customers = await _repo.SearchCustomersAsync(text);

        return customers.Select(x => new CustomerLookupModel
        {
            DocumentType = x.DocumentType,
            DocumentNumber = x.DocumentNumber,
            FullName = x.FullName,
            Nationality = x.Nationality,
            Age = x.Age
        }).ToList();
    }

    public async Task<ReservationDetailModel?> GetDetailAsync(Guid id)
    {
        var reservation = await _repo.GetDetailAsync(id);

        if (reservation is null)
            return null;

        return new ReservationDetailModel
        {
            Id = reservation.Id,
            ReservationCode = reservation.ReservationCode,
            Status = reservation.Status,
            PaymentStatus = reservation.PaymentStatus,
            Agency = reservation.Agency?.Name,
            Seller = reservation.Seller is null ? null : $"{reservation.Seller.FirstName} {reservation.Seller.LastName}",
            CreatedAt = reservation.CreatedAt,
            Passengers = reservation.Items.Select(x => new ReservationPassengerDetailModel
            {
                DocumentNumber = x.Customer.DocumentNumber,
                FullName = x.Customer.FullName,
                Age = x.Customer.Age,
                TripType = x.TripType,
                PassengerType = x.PassengerType,
                Status = x.Status,

                OutboundTrip = x.OutboundRouteSchedule is null
                    ? null
                    : $"{x.OutboundRouteSchedule.Route.Origin} - {x.OutboundRouteSchedule.Route.Destination} / {x.OutboundRouteSchedule.Schedule.Name}",

                OutboundDate = x.OutboundTravelDate,

                ReturnTrip = x.ReturnRouteSchedule is null
                    ? null
                    : $"{x.ReturnRouteSchedule.Route.Origin} - {x.ReturnRouteSchedule.Route.Destination} / {x.ReturnRouteSchedule.Schedule.Name}",

                ReturnDate = x.ReturnTravelDate
            }).ToList()
        };
    }

    private async Task ValidateAsync(CreateReservationModel model)
    {
        if (!model.Passengers.Any())
            throw new Exception("Debe ingresar al menos un pasajero.");

        if (model.Passengers.Any(x => !x.Outbound && !x.Return))
            throw new Exception("Cada pasajero debe tener ida, retorno o ambos.");

        if (model.Passengers.Any(x => string.IsNullOrWhiteSpace(x.DocumentNumber)))
            throw new Exception("Todos los pasajeros deben tener documento.");

        if (model.Passengers.Any(x => string.IsNullOrWhiteSpace(x.FullName)))
            throw new Exception("Todos los pasajeros deben tener nombres.");

        if (!model.OutboundRouteScheduleId.HasValue || !model.OutboundDate.HasValue)
            throw new Exception("Debe seleccionar viaje y fecha de la reserva.");

        var duplicatedDocuments = model.Passengers
            .Where(x => !string.IsNullOrWhiteSpace(x.DocumentNumber))
            .GroupBy(x => x.DocumentNumber.Trim().ToUpper())
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicatedDocuments.Any())
            throw new Exception($"Existen documentos duplicados: {string.Join(", ", duplicatedDocuments)}.");

        foreach (var passenger in model.Passengers)
        {
            passenger.PassengerType = NormalizePassengerType(passenger.PassengerType);

            if (!passenger.Age.HasValue || passenger.Age.Value < 0)
                throw new Exception($"El pasajero {passenger.FullName} debe tener una edad válida.");

            if (passenger.PassengerType == "Infant" && passenger.Age.Value > 1)
                throw new Exception($"El pasajero {passenger.FullName} no puede ser infante porque tiene más de 1 año.");

            if (passenger.Return && !passenger.ReturnDate.HasValue)
                passenger.ReturnDate = model.OutboundDate;

            if (passenger.PassengerType == "Infant")
            {
                if (!passenger.Age.HasValue)
                    throw new Exception($"El infante {passenger.FullName} debe tener Edad.");

                if (passenger.Age > 1)
                    throw new Exception($"El pasajero {passenger.FullName} no puede ser infante porque tiene más de 1 año.");
            }
        }

        await Task.CompletedTask;
    }

    private async Task<Customer> GetOrCreateCustomerAsync(
        ReservationPassengerModel passenger,
        Guid tenantId)
    {
        var document = passenger.DocumentNumber.Trim();
        var fullName = passenger.FullName.Trim();

        var customer = await _repo.GetCustomerByDocumentAsync(document);

        var age = passenger.Age;

        if (customer is not null)
        {
            customer.DocumentType = passenger.DocumentType;
            customer.FullName = fullName;
            customer.Nationality = passenger.Nationality; 
            customer.Age = age;
            return customer;
        }

        customer = new Customer
        {
            TenantId = tenantId,
            DocumentType = passenger.DocumentType,
            DocumentNumber = document,
            FullName = fullName,
            Nationality = passenger.Nationality,
            Age = age,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Customers.Add(customer);

        return customer;
    }

    private static string GetTripType(ReservationPassengerModel passenger)
    {
        if (passenger.Outbound && passenger.Return)
            return "RoundTrip";

        if (passenger.Outbound)
            return "Outbound";

        if (passenger.Return)
            return "Return";

        return "Outbound";
    }

    private async Task<string> GenerateReservationCodeAsync()
    {
        var count = await _repo.CountReservationsTodayAsync() + 1;
        return $"RSV-{DateTime.UtcNow:yyyyMMdd}-{count:0000}";
    }

    private static string NormalizePassengerType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Adult";

        return value.Trim() switch
        {
            "Adult" => "Adult",
            "Adulto" => "Adult",
            "Infant" => "Infant",
            "Infante" => "Infant",
            "Courtesy" => "Courtesy",
            "Cortesía" => "Courtesy",
            "Cortesia" => "Courtesy",
            _ => "Adult"
        };
    }

    public async Task<List<RouteListItem>> GetRoutesByBoatAsync(Guid boatId)
    {
        var routes = await _repo.GetRoutesByBoatAsync(boatId);

        return routes
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .Select(x => new RouteListItem
            {
                Id = x.Id,
                Origin = x.Origin,
                Destination = x.Destination
            })
            .OrderBy(x => x.Origin)
            .ThenBy(x => x.Destination)
            .ToList();
    }

    public async Task<List<WeeklyAvailabilityItem>> GetWeeklyAvailabilityAsync(
        DateTime weekStart,
        Guid? boatId,
        Guid? routeId)
    {
        if (!boatId.HasValue || !routeId.HasValue)
            return [];

        var schedules = await _repo.GetRouteSchedulesAsync(
            boatId.Value,
            routeId.Value);

        schedules = schedules
            .GroupBy(x => new
            {
                x.BoatId,
                x.RouteId,
                x.ScheduleId
            })
            .Select(x => x.First())
            .ToList();

        var result = new List<WeeklyAvailabilityItem>();

        for (var i = 0; i < 5; i++)
        {
            var date = weekStart.Date.AddDays(i);

            foreach (var schedule in schedules)
            {
                var reserved = await _repo.CountReservedAsync(
                    schedule.Id,
                    date,
                    outbound: true);

                result.Add(new WeeklyAvailabilityItem
                {
                    BoatRouteScheduleId = schedule.Id,
                    BoatId = schedule.BoatId,
                    Boat = schedule.Boat.Name,
                    Route = $"{schedule.Route.Origin} - {schedule.Route.Destination}",
                    Schedule = schedule.Schedule.Name,
                    TravelDate = date,
                    Capacity = schedule.Boat.Capacity,
                    ExtraCapacity = schedule.Boat.ExtraCapacity,
                    Reserved = reserved
                });
            }
        }

        return result
            .GroupBy(x => new
            {
                x.BoatRouteScheduleId,
                Date = x.TravelDate.Date
            })
            .Select(x => x.First())
            .OrderBy(x => x.TravelDate)
            .ThenBy(x => x.Schedule)
            .ToList();
    }

    public async Task<List<BoatListItem>> GetBoatsByRouteAsync(Guid routeId)
    {
        var boats = await _repo.GetBoatsByRouteAsync(routeId);

        return boats
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .Select(x => new BoatListItem
            {
                Id = x.Id,
                Name = x.Name
            })
            .OrderBy(x => x.Name)
            .ToList();
    }
    public async Task<Customer?> GetCustomerByDocumentAsync(string documentNumber)
    {
        return await _repo.GetCustomerByDocumentAsync(documentNumber);
    }
    public async Task<RouteScheduleInfoModel> GetRouteScheduleInfoAsync(Guid routeScheduleId)
    {
        var schedule = await _repo.GetRouteScheduleAsync(routeScheduleId);

        if (schedule is null)
            throw new Exception("Horario de viaje no encontrado.");

        return new RouteScheduleInfoModel
        {
            BoatId = schedule.BoatId,
            RouteId = schedule.RouteId
        };
    }
    public async Task<CreateReservationModel?> GetForEditAsync(Guid reservationId)
    {
        var reservation = await _repo.GetDetailAsync(reservationId);

        if (reservation is null)
            return null;

        var firstItem = reservation.Items.FirstOrDefault(x => x.Status != "Cancelled");

        return new CreateReservationModel
        {
            ExternalReference = reservation.ExternalReference,
            AgencyId = reservation.AgencyId,
            SellerId = reservation.SellerId,
            ContactName = reservation.ContactName,
            ContactPhone = reservation.ContactPhone,
            PaymentStatus = reservation.PaymentStatus,
            Notes = reservation.Notes,
            OutboundRouteScheduleId = firstItem?.OutboundRouteScheduleId,
            OutboundDate = firstItem?.OutboundTravelDate?.ToDateTime(TimeOnly.MinValue),

            Passengers = reservation.Items.Select(x => new ReservationPassengerModel
            {
                ReservationItemId = x.Id,
                CustomerId = x.CustomerId,
                DocumentType = x.Customer.DocumentType,
                DocumentNumber = x.Customer.DocumentNumber,
                FullName = x.Customer.FullName,
                Nationality = x.Customer.Nationality,
                Age = x.Customer.Age,
                PassengerType = x.PassengerType,
                Outbound = x.OutboundRouteScheduleId.HasValue,
                Return = x.ReturnRouteScheduleId.HasValue,
                ReturnDate = x.ReturnTravelDate?.ToDateTime(TimeOnly.MinValue),
                IsCancelled = x.Status == "Cancelled"
            }).ToList()
        };
    }
    public async Task CancelPassengerAsync(Guid reservationId, Guid itemId, string? reason)
    {
        var userId = await GetRequiredCurrentUserIdAsync();

        var reservation = await _repo.GetFullAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var item = reservation.Items.FirstOrDefault(x => x.Id == itemId);

        if (item is null)
            throw new Exception("Pasajero no encontrado.");

        if (item.Status == "Cancelled")
            throw new Exception("El pasajero ya se encuentra cancelado.");

        var oldReservationStatus = reservation.Status;
        var oldItemStatus = item.Status;

        item.Status = "Cancelled";
        reservation.UpdatedAt = DateTime.UtcNow;

        if (reservation.Items.All(x => x.Status == "Cancelled"))
            reservation.Status = "Cancelled";

        _db.ReservationHistory.Add(new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "CancelPassenger",
            Reason = string.IsNullOrWhiteSpace(reason)
                ? $"Pasajero cancelado: {item.Customer?.FullName}"
                : reason,
            OldStatus = oldReservationStatus,
            NewStatus = reservation.Status,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Reservation", "CancelPassenger", new
        {
            reservation.Id,
            ReservationStatus = oldReservationStatus,
            ItemId = item.Id,
            ItemStatus = oldItemStatus,
            CustomerId = item.CustomerId
        }, new
        {
            reservation.Id,
            ReservationStatus = reservation.Status,
            ItemId = item.Id,
            ItemStatus = item.Status,
            CustomerId = item.CustomerId
        });
    }
    public async Task ReplacePassengerAsync(
    Guid reservationId,
    Guid itemId,
    ReservationPassengerModel passenger)
    {
        var userId = await GetRequiredCurrentUserIdAsync();
        var tenantId = _tenant.GetTenantId();

        if (string.IsNullOrWhiteSpace(passenger.DocumentNumber))
            throw new Exception("Debe ingresar el documento del pasajero.");

        if (string.IsNullOrWhiteSpace(passenger.FullName))
            throw new Exception("Debe ingresar el nombre del pasajero.");

        var reservation = await _repo.GetFullAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var item = reservation.Items.FirstOrDefault(x => x.Id == itemId);

        if (item is null)
            throw new Exception("Pasajero no encontrado.");

        if (item.Status == "Cancelled")
            throw new Exception("No se puede reemplazar un pasajero cancelado.");

        var oldCustomerId = item.CustomerId;
        var oldPassengerType = item.PassengerType;
        var oldTripType = item.TripType;

        var customer = await GetOrCreateCustomerAsync(passenger, tenantId);

        item.CustomerId = customer.Id;
        item.PassengerType = NormalizePassengerType(passenger.PassengerType);
        item.TripType = GetTripType(passenger);

        reservation.UpdatedAt = DateTime.UtcNow;

        _db.ReservationHistory.Add(new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "ReplacePassenger",
            Reason = $"Pasajero reemplazado en item {itemId}",
            OldStatus = reservation.Status,
            NewStatus = reservation.Status,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Reservation", "ReplacePassenger", new
        {
            reservation.Id,
            ItemId = item.Id,
            CustomerId = oldCustomerId,
            PassengerType = oldPassengerType,
            TripType = oldTripType
        }, new
        {
            reservation.Id,
            ItemId = item.Id,
            CustomerId = item.CustomerId,
            PassengerType = item.PassengerType,
            TripType = item.TripType
        });
    }
    public async Task UpdateAsync(Guid reservationId, CreateReservationModel model)
    {
        await ValidateAsync(model);

        var tenantId = _tenant.GetTenantId();

        var reservation = await _repo.GetFullAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        reservation.ExternalReference = model.ExternalReference;
        reservation.AgencyId = model.AgencyId;
        reservation.SellerId = model.SellerId;
        reservation.ContactName = model.ContactName;
        reservation.ContactPhone = model.ContactPhone;
        reservation.PaymentStatus = model.PaymentStatus;
        reservation.Notes = model.Notes;
        reservation.UpdatedAt = DateTime.UtcNow;

        foreach (var passenger in model.Passengers.Where(x => !x.IsCancelled))
        {
            var customer = await GetOrCreateCustomerAsync(passenger, tenantId);

            if (passenger.ReservationItemId.HasValue)
            {
                var item = reservation.Items.First(x => x.Id == passenger.ReservationItemId.Value);

                item.CustomerId = customer.Id;
                item.PassengerType = NormalizePassengerType(passenger.PassengerType);
                item.TripType = GetTripType(passenger);
                item.OutboundRouteScheduleId = passenger.Outbound ? model.OutboundRouteScheduleId : null;
                item.OutboundTravelDate = passenger.Outbound && model.OutboundDate.HasValue
                    ? DateOnly.FromDateTime(model.OutboundDate.Value)
                    : null;
                item.ReturnRouteScheduleId = passenger.Return ? model.OutboundRouteScheduleId : null;
                item.ReturnTravelDate = passenger.Return && passenger.ReturnDate.HasValue
                    ? DateOnly.FromDateTime(passenger.ReturnDate.Value)
                    : null;
            }
            else
            {
                reservation.Items.Add(new ReservationItem
                {
                    TenantId = tenantId,
                    ReservationId = reservation.Id,
                    CustomerId = customer.Id,
                    PassengerType = NormalizePassengerType(passenger.PassengerType),
                    TripType = GetTripType(passenger),
                    OutboundRouteScheduleId = passenger.Outbound ? model.OutboundRouteScheduleId : null,
                    OutboundTravelDate = passenger.Outbound && model.OutboundDate.HasValue
                        ? DateOnly.FromDateTime(model.OutboundDate.Value)
                        : null,
                    ReturnRouteScheduleId = passenger.Return ? model.OutboundRouteScheduleId : null,
                    ReturnTravelDate = passenger.Return && passenger.ReturnDate.HasValue
                        ? DateOnly.FromDateTime(passenger.ReturnDate.Value)
                        : null,
                    Status = "Reserved",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
    }
    public async Task<List<ReservationOperationItem>> GetOperationsReservationsAsync(
    ReservationOperationsFilter filter)
    {
        var tenantId = _tenant.GetTenantId();

        var query = _db.Reservations
            .AsNoTracking()
            .Include(x => x.Agency)
            .Include(x => x.Seller)
            .Include(x => x.Comments)
            .Include(x => x.Items)
                .ThenInclude(x => x.OutboundRouteSchedule)
                    .ThenInclude(x => x!.Boat)
            .Include(x => x.Items)
                .ThenInclude(x => x.OutboundRouteSchedule)
                    .ThenInclude(x => x!.Route)
            .Include(x => x.Items)
                .ThenInclude(x => x.OutboundRouteSchedule)
                    .ThenInclude(x => x!.Schedule)
            .Where(x => x.TenantId == tenantId);

        if (filter.DateFrom.HasValue)
        {
            var date = DateOnly.FromDateTime(filter.DateFrom.Value.Date);
            query = query.Where(x => x.Items.Any(i =>
                i.OutboundTravelDate >= date ||
                i.ReturnTravelDate >= date));
        }

        if (filter.DateTo.HasValue)
        {
            var date = DateOnly.FromDateTime(filter.DateTo.Value.Date);
            query = query.Where(x => x.Items.Any(i =>
                i.OutboundTravelDate <= date ||
                i.ReturnTravelDate <= date));
        }

        if (filter.AgencyId.HasValue)
            query = query.Where(x => x.AgencyId == filter.AgencyId.Value);

        if (filter.SellerId.HasValue)
            query = query.Where(x => x.SellerId == filter.SellerId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => x.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.PaymentStatus))
            query = query.Where(x => x.PaymentStatus == filter.PaymentStatus);

        if (filter.BoatId.HasValue)
        {
            query = query.Where(x => x.Items.Any(i =>
                i.OutboundRouteSchedule != null &&
                i.OutboundRouteSchedule.BoatId == filter.BoatId.Value));
        }

        if (filter.RouteId.HasValue)
        {
            query = query.Where(x => x.Items.Any(i =>
                i.OutboundRouteSchedule != null &&
                i.OutboundRouteSchedule.RouteId == filter.RouteId.Value));
        }

        var data = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return data.Select(x =>
        {
            var firstItem = x.Items.FirstOrDefault(i => i.Status != "Cancelled");
            var outbound = firstItem?.OutboundRouteSchedule;

            return new ReservationOperationItem
            {
                Id = x.Id,
                ReservationCode = x.ReservationCode,
                ExternalReference = x.ExternalReference,
                Agency = x.Agency?.Name,
                Seller = x.Seller is null ? null : $"{x.Seller.FirstName} {x.Seller.LastName}".Trim(),
                Boat = outbound?.Boat?.Name,
                Route = outbound?.Route is null ? null : $"{outbound.Route.Origin} - {outbound.Route.Destination}",
                Schedule = outbound?.Schedule?.Name,
                TravelDate = firstItem?.OutboundTravelDate,
                Status = x.Status,
                PaymentStatus = x.PaymentStatus,
                PassengerCount = x.Items.Count(i => i.Status != "Cancelled"),
                TotalAmount = x.Items.Where(i => i.Status != "Cancelled").Sum(i => i.TotalPrice),
                ContactName = x.ContactName,
                ContactPhone = x.ContactPhone,
                Notes = x.Notes,
                LastComment = x.Comments
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => c.Comment)
                    .FirstOrDefault(),
                CreatedAt = x.CreatedAt
            };
        }).ToList();
    }
    public async Task<List<ReservationPassengerOperationItem>> GetOperationsPassengersAsync(
    ReservationOperationsFilter filter)
    {
        var tenantId = _tenant.GetTenantId();

        var query = _db.ReservationItems
            .AsNoTracking()
            .Include(x => x.Reservation)
                .ThenInclude(x => x.Agency)
            .Include(x => x.Reservation)
                .ThenInclude(x => x.Seller)
            .Include(x => x.Customer)
            .Include(x => x.OutboundRouteSchedule)
                .ThenInclude(x => x!.Boat)
            .Include(x => x.OutboundRouteSchedule)
                .ThenInclude(x => x!.Route)
            .Include(x => x.OutboundRouteSchedule)
                .ThenInclude(x => x!.Schedule)
            .Include(x => x.ReturnRouteSchedule)
                .ThenInclude(x => x!.Boat)
            .Include(x => x.ReturnRouteSchedule)
                .ThenInclude(x => x!.Route)
            .Include(x => x.ReturnRouteSchedule)
                .ThenInclude(x => x!.Schedule)
            .Where(x => x.TenantId == tenantId);

        if (filter.DateFrom.HasValue)
        {
            var date = DateOnly.FromDateTime(filter.DateFrom.Value.Date);
            query = query.Where(x =>
                x.OutboundTravelDate >= date ||
                x.ReturnTravelDate >= date);
        }

        if (filter.DateTo.HasValue)
        {
            var date = DateOnly.FromDateTime(filter.DateTo.Value.Date);
            query = query.Where(x =>
                x.OutboundTravelDate <= date ||
                x.ReturnTravelDate <= date);
        }

        if (filter.AgencyId.HasValue)
            query = query.Where(x => x.Reservation.AgencyId == filter.AgencyId.Value);

        if (filter.SellerId.HasValue)
            query = query.Where(x => x.Reservation.SellerId == filter.SellerId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => x.Reservation.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.PaymentStatus))
            query = query.Where(x => x.Reservation.PaymentStatus == filter.PaymentStatus);

        if (filter.BoatId.HasValue)
        {
            query = query.Where(x =>
                (x.OutboundRouteSchedule != null && x.OutboundRouteSchedule.BoatId == filter.BoatId.Value) ||
                (x.ReturnRouteSchedule != null && x.ReturnRouteSchedule.BoatId == filter.BoatId.Value));
        }

        if (filter.RouteId.HasValue)
        {
            query = query.Where(x =>
                (x.OutboundRouteSchedule != null && x.OutboundRouteSchedule.RouteId == filter.RouteId.Value) ||
                (x.ReturnRouteSchedule != null && x.ReturnRouteSchedule.RouteId == filter.RouteId.Value));
        }

        var data = await query
            .OrderByDescending(x => x.OutboundTravelDate)
            .ThenBy(x => x.Reservation.ReservationCode)
            .ThenBy(x => x.Customer.FullName)
            .ToListAsync();

        return data.Select(x => new ReservationPassengerOperationItem
        {
            ReservationId = x.ReservationId,
            ReservationItemId = x.Id,
            ReservationCode = x.Reservation.ReservationCode,
            ExternalReference = x.Reservation.ExternalReference,
            Agency = x.Reservation.Agency?.Name,
            Seller = x.Reservation.Seller is null
                ? null
                : $"{x.Reservation.Seller.FirstName} {x.Reservation.Seller.LastName}".Trim(),

            DocumentType = x.Customer.DocumentType,
            DocumentNumber = x.Customer.DocumentNumber,
            FullName = x.Customer.FullName,
            Nationality = x.Customer.Nationality,
            Age = x.Customer.Age,

            PassengerType = x.PassengerType,
            TripType = x.TripType,

            OutboundBoat = x.OutboundRouteSchedule?.Boat?.Name,
            OutboundRoute = x.OutboundRouteSchedule?.Route is null
                ? null
                : $"{x.OutboundRouteSchedule.Route.Origin} - {x.OutboundRouteSchedule.Route.Destination}",
            OutboundSchedule = x.OutboundRouteSchedule?.Schedule?.Name,
            OutboundTravelDate = x.OutboundTravelDate,

            ReturnBoat = x.ReturnRouteSchedule?.Boat?.Name,
            ReturnRoute = x.ReturnRouteSchedule?.Route is null
                ? null
                : $"{x.ReturnRouteSchedule.Route.Origin} - {x.ReturnRouteSchedule.Route.Destination}",
            ReturnSchedule = x.ReturnRouteSchedule?.Schedule?.Name,
            ReturnTravelDate = x.ReturnTravelDate,

            Status = x.Status,
            PaymentStatus = x.Reservation.PaymentStatus
        }).ToList();
    }
    public async Task AddCommentAsync(AddReservationCommentModel model)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        if (string.IsNullOrWhiteSpace(model.Comment))
            throw new Exception("Debe ingresar un comentario.");

        var reservation = await _db.Reservations
            .FirstOrDefaultAsync(x => x.Id == model.ReservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var oldPaymentStatus = reservation.PaymentStatus;

        if (!string.IsNullOrWhiteSpace(model.PaymentStatus))
        {
            reservation.PaymentStatus = model.PaymentStatus;
            reservation.UpdatedAt = DateTime.UtcNow;
        }

        var comment = new ReservationComment
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            CommentType = model.CommentType,
            Comment = model.Comment.Trim(),
            PaymentStatus = reservation.PaymentStatus,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.ReservationComments.Add(comment);

        _db.ReservationHistory.Add(new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "AddComment",
            Reason = model.Comment,
            OldStatus = oldPaymentStatus,
            NewStatus = reservation.PaymentStatus,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Reservation", "AddComment", new
        {
            reservation.Id,
            PaymentStatus = oldPaymentStatus
        }, new
        {
            reservation.Id,
            reservation.PaymentStatus,
            model.CommentType,
            model.Comment
        });
    }
    public async Task ChangeReservationBoatAsync(ChangeReservationBoatModel model)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        if (!model.ApplyToOutbound && !model.ApplyToReturn)
            throw new Exception("Debe seleccionar ida, retorno o ambos.");

        if (string.IsNullOrWhiteSpace(model.Reason))
            throw new Exception("Debe ingresar el motivo del cambio.");

        var newSchedule = await _db.BoatRouteSchedules
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .FirstOrDefaultAsync(x => x.Id == model.NewBoatRouteScheduleId && x.IsActive);

        if (newSchedule is null)
            throw new Exception("La nueva embarcación/horario no existe o está inactiva.");

        var reservation = await _db.Reservations
            .Include(x => x.Items)
                .ThenInclude(x => x.OutboundRouteSchedule)
                    .ThenInclude(x => x!.Boat)
            .Include(x => x.Items)
                .ThenInclude(x => x.ReturnRouteSchedule)
                    .ThenInclude(x => x!.Boat)
            .FirstOrDefaultAsync(x => x.Id == model.ReservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var oldValues = reservation.Items.Select(x => new
        {
            x.Id,
            x.OutboundRouteScheduleId,
            OutboundBoat = x.OutboundRouteSchedule?.Boat?.Name,
            x.ReturnRouteScheduleId,
            ReturnBoat = x.ReturnRouteSchedule?.Boat?.Name
        }).ToList();

        foreach (var item in reservation.Items.Where(x => x.Status != "Cancelled"))
        {
            if (model.ApplyToOutbound && item.OutboundRouteScheduleId.HasValue)
                item.OutboundRouteScheduleId = model.NewBoatRouteScheduleId;

            if (model.ApplyToReturn && item.ReturnRouteScheduleId.HasValue)
                item.ReturnRouteScheduleId = model.NewBoatRouteScheduleId;
        }

        reservation.UpdatedAt = DateTime.UtcNow;

        _db.ReservationHistory.Add(new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "ChangeBoat",
            Reason = model.Reason,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Reservation", "ChangeBoat", oldValues, new
        {
            reservation.Id,
            reservation.ReservationCode,
            NewBoatRouteScheduleId = model.NewBoatRouteScheduleId,
            Boat = newSchedule.Boat.Name,
            Route = $"{newSchedule.Route.Origin} - {newSchedule.Route.Destination}",
            Schedule = newSchedule.Schedule.Name,
            model.ApplyToOutbound,
            model.ApplyToReturn,
            model.Reason
        });
    }
    public async Task FinishWithCrewAsync(FinishReservationModel model)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        if (string.IsNullOrWhiteSpace(model.CaptainName))
            throw new Exception("Debe ingresar el capitán.");

        if (!model.ApplyToOutbound && !model.ApplyToReturn)
            throw new Exception("Debe seleccionar ida, retorno o ambos.");

        var reservation = await _db.Reservations
            .Include(x => x.Items)
            .Include(x => x.TripCrews)
            .FirstOrDefaultAsync(x => x.Id == model.ReservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        if (reservation.Status == "Cancelled")
            throw new Exception("No se puede finiquitar una reserva cancelada.");

        var oldStatus = reservation.Status;

        if (model.ApplyToOutbound)
        {
            UpsertTripCrew(reservation, model, "Outbound", user.UserId);
        }

        if (model.ApplyToReturn)
        {
            UpsertTripCrew(reservation, model, "Return", user.UserId);
        }

        reservation.Status = "Finished";
        reservation.UpdatedAt = DateTime.UtcNow;

        foreach (var item in reservation.Items.Where(x => x.Status != "Cancelled"))
            item.Status = "Finished";

        _db.ReservationHistory.Add(new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "FinishWithCrew",
            Reason = model.Notes,
            OldStatus = oldStatus,
            NewStatus = reservation.Status,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Reservation", "FinishWithCrew", new
        {
            reservation.Id,
            OldStatus = oldStatus
        }, new
        {
            reservation.Id,
            reservation.ReservationCode,
            reservation.Status,
            model.CaptainName,
            model.CaptainDocument,
            model.Sailor1Name,
            model.Sailor2Name,
            model.Sailor3Name,
            model.ApplyToOutbound,
            model.ApplyToReturn,
            model.Notes
        });
    }
    private static void UpsertTripCrew(
    Reservation reservation,
    FinishReservationModel model,
    string tripType,
    Guid userId)
    {
        var crew = reservation.TripCrews
            .FirstOrDefault(x => x.TripType == tripType);

        if (crew is null)
        {
            crew = new ReservationTripCrew
            {
                TenantId = reservation.TenantId,
                ReservationId = reservation.Id,
                TripType = tripType,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            reservation.TripCrews.Add(crew);
        }

        crew.CaptainName = model.CaptainName.Trim();
        crew.CaptainDocument = model.CaptainDocument;
        crew.Sailor1Name = model.Sailor1Name;
        crew.Sailor1Document = model.Sailor1Document;
        crew.Sailor2Name = model.Sailor2Name;
        crew.Sailor2Document = model.Sailor2Document;
        crew.Sailor3Name = model.Sailor3Name;
        crew.Sailor3Document = model.Sailor3Document;
        crew.Notes = model.Notes;
    }
    public async Task<List<CatalogItem>> GetOperationBoatsAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Boats
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CatalogItem
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync();
    }

    public async Task<List<CatalogItem>> GetOperationAgenciesAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Agencies
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CatalogItem
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync();
    }

    public async Task<List<CatalogItem>> GetOperationSellersAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Sellers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .Select(x => new CatalogItem
            {
                Id = x.Id,
                Name = $"{x.FirstName} {x.LastName}".Trim()
            })
            .ToListAsync();
    }
    public async Task<List<RouteScheduleCatalogItem>> GetOperationRouteSchedulesAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.BoatRouteSchedules
            .AsNoTracking()
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Boat.Name)
            .ThenBy(x => x.Route.Origin)
            .ThenBy(x => x.Schedule.DepartureTime)
            .Select(x => new RouteScheduleCatalogItem
            {
                Id = x.Id,
                BoatId = x.BoatId,
                RouteId = x.RouteId,
                Boat = x.Boat.Name,
                Route = x.Route.Origin + " - " + x.Route.Destination,
                Schedule = x.Schedule.Name
            })
            .ToListAsync();
    }
    private async Task<Guid> GetRequiredCurrentUserIdAsync()
    {
        var user = await _currentUser.LoadAsync();

        if (user is null || user.UserId == Guid.Empty)
            throw new Exception("Usuario no autenticado.");

        var exists = await _db.Users.AnyAsync(x => x.Id == user.UserId);

        if (!exists)
            throw new Exception("El usuario autenticado no existe en la tabla Users.");

        return user.UserId;
    }
}