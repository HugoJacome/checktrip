using CheckAccess.Features.Reservations.Models;
using CheckAccess.Infrastructure.Auth;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Features.Reservations.Models;
using CheckTrip.Web.Features.Reservations.Models.Operations;
using CheckTrip.Web.Infrastructure.Audit;
using CheckTrip.Web.Infrastructure.Auth;
using CheckTrip.Web.Infrastructure.Repositories;
using CheckTrip.Web.Infrastructure.Tenant;

namespace CheckTrip.Web.Features.Reservations.Services;

public class ReservationService
{
    private readonly ReservationRepository _repo;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public ReservationService(
        ReservationRepository repo,
        ITenantProvider tenant,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
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
            PassengerCount = CountUniquePassengers(x.PassengerTrips),
            ContactName = x.ContactName,
            ContactPhone = x.ContactPhone
        }).ToList();
    }

    public async Task<AvailabilityResult> GetAvailabilityAsync(Guid routeScheduleId, DateTime date, bool outbound)
    {
        var schedule = await _repo.GetRouteScheduleAsync(routeScheduleId);

        if (schedule is null)
            throw new Exception("Configuración de viaje no encontrada.");

        var reserved = await _repo.CountReservedPassengerTripsAsync(routeScheduleId, date, reservationIdToExclude: null);

        return new AvailabilityResult
        {
            BoatRouteScheduleId = routeScheduleId,
            TravelDate = date.Date,
            Capacity = schedule.Boat.Capacity,
            ExtraCapacity = schedule.Boat.ExtraCapacity,
            Reserved = reserved
        };
    }

    public async Task<List<WeeklyAvailabilityItem>> GetWeeklyAvailabilityAsync(DateTime weekStart, Guid? routeId)
    {
        var schedules = await _repo.GetRouteSchedulesByRouteAsync(routeId);

        schedules = schedules
            .GroupBy(x => new { x.RouteId, x.BoatId, x.ScheduleId })
            .Select(x => x.First())
            .ToList();

        var result = new List<WeeklyAvailabilityItem>();

        for (var i = 0; i < 5; i++)
        {
            var date = weekStart.Date.AddDays(i);

            foreach (var schedule in schedules)
            {
                var reserved = await _repo.CountReservedPassengerTripsAsync(schedule.Id, date, reservationIdToExclude: null);

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
        await ValidateCapacityAsync(model, reservationIdToExclude: null);

        var tenantId = _tenant.GetTenantId();
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var reservation = new Reservation
        {
            TenantId = tenantId,
            ReservationCode = await _repo.GenerateReservationCodeAsync(),
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
            PassengerTrips = []
        };

        foreach (var passenger in model.Passengers.Where(x => !x.IsCancelled))
        {
            NormalizeGenericPassenger(passenger);

            var customer = passenger.IsGenericPassenger
                ? null
                : await _repo.GetOrCreateCustomerAsync(passenger);

            AddPassengerTrips(
                reservation,
                passenger,
                customer,
                model.OutboundRouteScheduleId!.Value,
                model.OutboundDate!.Value);
        }

        var history = new ReservationHistory
        {
            TenantId = tenantId,
            Reservation = reservation,
            Action = "Create",
            NewStatus = reservation.Status,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        };

        var id = await _repo.CreateReservationAsync(reservation, history);

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
            PassengerTrips = reservation.PassengerTrips.Select(ToAuditTrip).ToList()
        });

        return id;
    }

    public async Task UpdateAsync(Guid reservationId, CreateReservationModel model)
    {
        await ValidateAsync(model);
        await ValidateCapacityAsync(model, reservationIdToExclude: reservationId);

        var tenantId = _tenant.GetTenantId();
        var userId = await GetRequiredCurrentUserIdAsync();

        var reservation = await _repo.GetReservationWithPassengerTripsAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var oldValues = new
        {
            reservation.ExternalReference,
            reservation.AgencyId,
            reservation.SellerId,
            reservation.ContactName,
            reservation.ContactPhone,
            reservation.PaymentStatus,
            reservation.Notes,
            PassengerTrips = reservation.PassengerTrips.Select(ToAuditTrip).ToList()
        };

        reservation.ExternalReference = model.ExternalReference;
        reservation.AgencyId = model.AgencyId;
        reservation.SellerId = model.SellerId;
        reservation.ContactName = model.ContactName;
        reservation.ContactPhone = model.ContactPhone;
        reservation.PaymentStatus = model.PaymentStatus;
        reservation.Notes = model.Notes;
        reservation.UpdatedAt = DateTime.UtcNow;

        foreach (var oldTrip in reservation.PassengerTrips.Where(x => x.Status != "Cancelled"))
        {
            oldTrip.Status = "Cancelled";
            oldTrip.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var passenger in model.Passengers.Where(x => !x.IsCancelled))
        {
            NormalizeGenericPassenger(passenger);

            var customer = passenger.IsGenericPassenger
                ? null
                : await _repo.GetOrCreateCustomerAsync(passenger);

            AddPassengerTrips(
                reservation,
                passenger,
                customer,
                model.OutboundRouteScheduleId!.Value,
                model.OutboundDate!.Value);
        }

        var history = new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "Update",
            OldStatus = reservation.Status,
            NewStatus = reservation.Status,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.SaveReservationWithHistoryAsync(reservation, history);

        await _audit.LogAsync("Reservation", "Update", oldValues, new
        {
            reservation.Id,
            reservation.ExternalReference,
            reservation.AgencyId,
            reservation.SellerId,
            reservation.ContactName,
            reservation.ContactPhone,
            reservation.PaymentStatus,
            reservation.Notes,
            PassengerTrips = reservation.PassengerTrips.Select(ToAuditTrip).ToList()
        });
    }

    public async Task CancelAsync(Guid reservationId, string? reason)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var reservation = await _repo.GetReservationWithPassengerTripsAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var oldStatus = reservation.Status;

        reservation.Status = "Cancelled";
        reservation.UpdatedAt = DateTime.UtcNow;

        foreach (var trip in reservation.PassengerTrips)
        {
            trip.Status = "Cancelled";
            trip.UpdatedAt = DateTime.UtcNow;
        }

        var history = new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "Cancel",
            Reason = reason,
            OldStatus = oldStatus,
            NewStatus = reservation.Status,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.SaveReservationWithHistoryAsync(reservation, history);

        await _audit.LogAsync("Reservation", "Cancel", new { oldStatus }, new
        {
            reservation.Id,
            reservation.ReservationCode,
            reservation.Status,
            reservation.UpdatedAt,
            PassengerTrips = reservation.PassengerTrips.Select(ToAuditTrip).ToList()
        });
    }

    public async Task FinishAsync(Guid reservationId)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var reservation = await _repo.GetReservationWithPassengerTripsAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var oldStatus = reservation.Status;

        reservation.Status = "Finished";
        reservation.UpdatedAt = DateTime.UtcNow;

        foreach (var trip in reservation.PassengerTrips.Where(x => x.Status != "Cancelled"))
        {
            trip.Status = "Finished";
            trip.UpdatedAt = DateTime.UtcNow;
        }

        var history = new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "Finish",
            OldStatus = oldStatus,
            NewStatus = reservation.Status,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.SaveReservationWithHistoryAsync(reservation, history);

        await _audit.LogAsync("Reservation", "Finish", new { oldStatus }, new
        {
            reservation.Id,
            reservation.ReservationCode,
            reservation.Status,
            reservation.UpdatedAt,
            PassengerTrips = reservation.PassengerTrips.Select(ToAuditTrip).ToList()
        });
    }

    public async Task ChangePaymentStatusAsync(Guid reservationId, string paymentStatus)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var reservation = await _repo.GetReservationWithPassengerTripsAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var old = reservation.PaymentStatus;

        reservation.PaymentStatus = paymentStatus;
        reservation.UpdatedAt = DateTime.UtcNow;

        var history = new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "ChangePaymentStatus",
            Reason = $"Cambio de pago: {old} -> {paymentStatus}",
            OldStatus = old,
            NewStatus = paymentStatus,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.SaveReservationWithHistoryAsync(reservation, history);

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
            Passengers = reservation.PassengerTrips.Select(x => new ReservationPassengerDetailModel
            {
                DocumentNumber = GetPassengerDocument(x),
                FullName = GetPassengerName(x),
                Age = x.Customer?.Age,
                TripType = x.SegmentType,
                PassengerType = x.PassengerType,
                Status = x.Status,
                OutboundTrip = x.SegmentType == "Outbound"
                    ? $"{x.BoatRouteSchedule.Route.Origin} - {x.BoatRouteSchedule.Route.Destination} / {x.BoatRouteSchedule.Schedule.Name}"
                    : null,
                OutboundDate = x.SegmentType == "Outbound" ? x.TravelDate : null,
                ReturnTrip = x.SegmentType == "Return"
                    ? $"{x.BoatRouteSchedule.Route.Origin} - {x.BoatRouteSchedule.Route.Destination} / {x.BoatRouteSchedule.Schedule.Name}"
                    : null,
                ReturnDate = x.SegmentType == "Return" ? x.TravelDate : null
            }).ToList()
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

    public async Task<List<WeeklyAvailabilityItem>> GetWeeklyAvailabilityAsync(DateTime weekStart, Guid? boatId, Guid? routeId)
    {
        if (!boatId.HasValue || !routeId.HasValue)
            return [];

        var schedules = await _repo.GetRouteSchedulesAsync(boatId.Value, routeId.Value);

        schedules = schedules
            .GroupBy(x => new { x.BoatId, x.RouteId, x.ScheduleId })
            .Select(x => x.First())
            .ToList();

        var result = new List<WeeklyAvailabilityItem>();

        for (var i = 0; i < 7; i++)
        {
            var date = weekStart.Date.AddDays(i);

            foreach (var schedule in schedules)
            {
                var reserved = await _repo.CountReservedPassengerTripsAsync(schedule.Id, date, reservationIdToExclude: null);

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
            .GroupBy(x => new { x.BoatRouteScheduleId, Date = x.TravelDate.Date })
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

        var activeTrips = reservation.PassengerTrips
            .Where(x => x.Status != "Cancelled")
            .ToList();

        var firstTrip = activeTrips
            .OrderBy(x => x.TravelDate)
            .FirstOrDefault();

        var passengers = activeTrips
            .GroupBy(GetPassengerGroupKey)
            .Select(g =>
            {
                var outbound = g.FirstOrDefault(x => x.SegmentType == "Outbound");
                var ret = g.FirstOrDefault(x => x.SegmentType == "Return");
                var first = outbound ?? ret ?? g.First();

                return new ReservationPassengerModel
                {
                    ReservationItemId = first.Id,
                    CustomerId = first.CustomerId,
                    DocumentType = first.Customer?.DocumentType ?? "Generico",
                    DocumentNumber = GetPassengerDocument(first),
                    FullName = GetPassengerName(first),
                    Nationality = first.Customer?.Nationality ?? "Ecuatoriana",
                    Age = first.Customer?.Age ?? 18,
                    PassengerType = first.PassengerType,
                    Outbound = outbound is not null,
                    Return = ret is not null,
                    ReturnDate = ret?.TravelDate.ToDateTime(TimeOnly.MinValue),
                    IsCancelled = false,
                    IsGenericPassenger = !first.CustomerId.HasValue
                };
            })
            .ToList();

        return new CreateReservationModel
        {
            ExternalReference = reservation.ExternalReference,
            AgencyId = reservation.AgencyId,
            SellerId = reservation.SellerId,
            ContactName = reservation.ContactName,
            ContactPhone = reservation.ContactPhone,
            PaymentStatus = reservation.PaymentStatus,
            Notes = reservation.Notes,
            OutboundRouteScheduleId = firstTrip?.BoatRouteScheduleId,
            OutboundDate = firstTrip?.TravelDate.ToDateTime(TimeOnly.MinValue),
            Passengers = passengers
        };
    }

    public async Task CancelPassengerAsync(Guid reservationId, Guid tripId, string? reason)
    {
        var userId = await GetRequiredCurrentUserIdAsync();

        var reservation = await _repo.GetReservationWithPassengerTripsAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var trip = reservation.PassengerTrips.FirstOrDefault(x => x.Id == tripId);

        if (trip is null)
            throw new Exception("Pasajero no encontrado.");

        var oldReservationStatus = reservation.Status;
        var key = GetPassengerGroupKey(trip);

        var passengerTrips = reservation.PassengerTrips
            .Where(x => GetPassengerGroupKey(x) == key && x.Status != "Cancelled")
            .ToList();

        if (!passengerTrips.Any())
            throw new Exception("El pasajero ya se encuentra cancelado.");

        foreach (var passengerTrip in passengerTrips)
        {
            passengerTrip.Status = "Cancelled";
            passengerTrip.UpdatedAt = DateTime.UtcNow;
        }

        reservation.UpdatedAt = DateTime.UtcNow;

        if (reservation.PassengerTrips.All(x => x.Status == "Cancelled"))
            reservation.Status = "Cancelled";

        var history = new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "CancelPassenger",
            Reason = string.IsNullOrWhiteSpace(reason)
                ? $"Pasajero cancelado: {GetPassengerName(trip)}"
                : reason,
            OldStatus = oldReservationStatus,
            NewStatus = reservation.Status,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.SaveReservationWithHistoryAsync(reservation, history);

        await _audit.LogAsync("Reservation", "CancelPassenger", new
        {
            reservation.Id,
            ReservationStatus = oldReservationStatus,
            PassengerKey = key
        }, new
        {
            reservation.Id,
            ReservationStatus = reservation.Status,
            PassengerKey = key,
            CancelledTripIds = passengerTrips.Select(x => x.Id).ToList()
        });
    }

    public async Task ReplacePassengerAsync(Guid reservationId, Guid tripId, ReservationPassengerModel passenger)
    {
        var userId = await GetRequiredCurrentUserIdAsync();

        if (!passenger.IsGenericPassenger && string.IsNullOrWhiteSpace(passenger.DocumentNumber))
            throw new Exception("Debe ingresar el documento del pasajero.");

        if (string.IsNullOrWhiteSpace(passenger.FullName))
            throw new Exception("Debe ingresar el nombre del pasajero.");

        var reservation = await _repo.GetReservationWithPassengerTripsAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var currentTrip = reservation.PassengerTrips.FirstOrDefault(x => x.Id == tripId);

        if (currentTrip is null)
            throw new Exception("Pasajero no encontrado.");

        if (currentTrip.Status == "Cancelled")
            throw new Exception("No se puede reemplazar un pasajero cancelado.");

        var oldKey = GetPassengerGroupKey(currentTrip);

        var tripsToReplace = reservation.PassengerTrips
            .Where(x => GetPassengerGroupKey(x) == oldKey && x.Status != "Cancelled")
            .ToList();

        NormalizeGenericPassenger(passenger);

        var customer = passenger.IsGenericPassenger
            ? null
            : await _repo.GetOrCreateCustomerAsync(passenger);

        var oldValues = tripsToReplace.Select(ToAuditTrip).ToList();

        foreach (var trip in tripsToReplace)
        {
            trip.CustomerId = customer?.Id;
            trip.GenericPassengerName = passenger.IsGenericPassenger ? passenger.FullName : null;
            trip.GenericDocumentNumber = passenger.IsGenericPassenger ? passenger.DocumentNumber : null;
            trip.PassengerType = NormalizePassengerType(passenger.PassengerType);
            trip.UpdatedAt = DateTime.UtcNow;
        }

        reservation.UpdatedAt = DateTime.UtcNow;

        var history = new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "ReplacePassenger",
            Reason = $"Pasajero reemplazado en grupo {oldKey}",
            OldStatus = reservation.Status,
            NewStatus = reservation.Status,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.SaveReservationWithHistoryAsync(reservation, history);

        await _audit.LogAsync("Reservation", "ReplacePassenger", oldValues, tripsToReplace.Select(ToAuditTrip).ToList());
    }

    public async Task<List<ReservationOperationItem>> GetOperationsReservationsAsync(ReservationOperationsFilter filter)
    {
        var data = await _repo.GetOperationsReservationsRawAsync(filter);

        return data.Select(x =>
        {
            var firstTrip = x.PassengerTrips.FirstOrDefault(i => i.Status != "Cancelled");
            var routeSchedule = firstTrip?.BoatRouteSchedule;

            return new ReservationOperationItem
            {
                Id = x.Id,
                ReservationCode = x.ReservationCode,
                ExternalReference = x.ExternalReference,
                Agency = x.Agency?.Name,
                Seller = x.Seller is null ? null : $"{x.Seller.FirstName} {x.Seller.LastName}".Trim(),
                Boat = routeSchedule?.Boat?.Name,
                Route = routeSchedule?.Route is null ? null : $"{routeSchedule.Route.Origin} - {routeSchedule.Route.Destination}",
                Schedule = routeSchedule?.Schedule?.Name,
                TravelDate = firstTrip?.TravelDate,
                Status = x.Status,
                PaymentStatus = x.PaymentStatus,
                PassengerCount = CountUniquePassengers(x.PassengerTrips),
                TotalAmount = x.PassengerTrips.Where(i => i.Status != "Cancelled").Sum(i => i.TotalPrice),
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

    public async Task<List<ReservationPassengerOperationItem>> GetOperationsPassengersAsync(ReservationOperationsFilter filter)
    {
        var data = await _repo.GetOperationsPassengersRawAsync(filter);

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

            DocumentType = x.Customer?.DocumentType ?? "Generico",
            DocumentNumber = GetPassengerDocument(x),
            FullName = GetPassengerName(x),
            Nationality = x.Customer?.Nationality ?? "Ecuatoriana",
            Age = x.Customer?.Age ?? 18,

            PassengerType = x.PassengerType,
            TripType = x.SegmentType,

            OutboundBoat = x.SegmentType == "Outbound" ? x.BoatRouteSchedule.Boat?.Name : null,
            OutboundRoute = x.SegmentType == "Outbound"
                ? $"{x.BoatRouteSchedule.Route.Origin} - {x.BoatRouteSchedule.Route.Destination}"
                : null,
            OutboundSchedule = x.SegmentType == "Outbound" ? x.BoatRouteSchedule.Schedule?.Name : null,
            OutboundTravelDate = x.SegmentType == "Outbound" ? x.TravelDate : null,

            ReturnBoat = x.SegmentType == "Return" ? x.BoatRouteSchedule.Boat?.Name : null,
            ReturnRoute = x.SegmentType == "Return"
                ? $"{x.BoatRouteSchedule.Route.Origin} - {x.BoatRouteSchedule.Route.Destination}"
                : null,
            ReturnSchedule = x.SegmentType == "Return" ? x.BoatRouteSchedule.Schedule?.Name : null,
            ReturnTravelDate = x.SegmentType == "Return" ? x.TravelDate : null,

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

        var reservation = await _repo.GetReservationForCommentAsync(model.ReservationId);

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

        var history = new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "AddComment",
            Reason = model.Comment,
            OldStatus = oldPaymentStatus,
            NewStatus = reservation.PaymentStatus,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.AddCommentAsync(reservation, comment, history);

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

        var newSchedule = await _repo.GetActiveRouteScheduleWithDetailAsync(model.NewBoatRouteScheduleId);

        if (newSchedule is null)
            throw new Exception("La nueva embarcación/horario no existe o está inactiva.");

        var reservation = await _repo.GetReservationForBoatChangeAsync(model.ReservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        var oldValues = reservation.PassengerTrips.Select(x => new
        {
            x.Id,
            x.BoatRouteScheduleId,
            Boat = x.BoatRouteSchedule?.Boat?.Name,
            x.SegmentType,
            x.TravelDate
        }).ToList();

        foreach (var trip in reservation.PassengerTrips.Where(x => x.Status != "Cancelled"))
        {
            if (model.ApplyToOutbound && trip.SegmentType == "Outbound")
                trip.BoatRouteScheduleId = model.NewBoatRouteScheduleId;

            if (model.ApplyToReturn && trip.SegmentType == "Return")
                trip.BoatRouteScheduleId = model.NewBoatRouteScheduleId;
        }

        reservation.UpdatedAt = DateTime.UtcNow;

        var history = new ReservationHistory
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            Action = "ChangeBoat",
            Reason = model.Reason,
            CreatedByUserId = user.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.SaveBoatChangeAsync(reservation, history);

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
        await FinishAsync(model.ReservationId);
    }

    public async Task<List<CatalogItem>> GetOperationBoatsAsync()
    {
        return await _repo.GetOperationBoatsAsync();
    }

    public async Task<List<CatalogItem>> GetOperationAgenciesAsync()
    {
        return await _repo.GetOperationAgenciesAsync();
    }

    public async Task<List<CatalogItem>> GetOperationSellersAsync()
    {
        return await _repo.GetOperationSellersAsync();
    }

    public async Task<List<RouteScheduleCatalogItem>> GetOperationRouteSchedulesAsync()
    {
        return await _repo.GetOperationRouteSchedulesAsync();
    }

    private async Task ValidateAsync(CreateReservationModel model)
    {
        if (!model.Passengers.Any(x => !x.IsCancelled))
            throw new Exception("Debe ingresar al menos un pasajero.");

        if (model.Passengers.Where(x => !x.IsCancelled).Any(x => !x.Outbound && !x.Return))
            throw new Exception("Cada pasajero debe tener ida, retorno o ambos.");

        if (!model.OutboundRouteScheduleId.HasValue || !model.OutboundDate.HasValue)
            throw new Exception("Debe seleccionar viaje y fecha de la reserva.");

        foreach (var passenger in model.Passengers.Where(x => !x.IsCancelled))
        {
            NormalizeGenericPassenger(passenger);
            passenger.PassengerType = NormalizePassengerType(passenger.PassengerType);

            if (!passenger.IsGenericPassenger)
            {
                if (string.IsNullOrWhiteSpace(passenger.DocumentNumber))
                    throw new Exception("Todos los pasajeros deben tener documento.");

                if (string.IsNullOrWhiteSpace(passenger.FullName))
                    throw new Exception("Todos los pasajeros deben tener nombres.");
            }

            if (string.IsNullOrWhiteSpace(passenger.FullName))
                passenger.FullName = "Pasajero genérico";

            if (!passenger.Age.HasValue || passenger.Age.Value < 0)
                throw new Exception($"El pasajero {passenger.FullName} debe tener una edad válida.");

            if (passenger.PassengerType == "Infant" && passenger.Age.Value > 1)
                throw new Exception($"El pasajero {passenger.FullName} no puede ser infante porque tiene más de 1 año.");

            if (passenger.Return && !passenger.ReturnDate.HasValue)
                passenger.ReturnDate = model.OutboundDate;
        }

        var duplicatedDocuments = model.Passengers
            .Where(x => !x.IsCancelled)
            .Where(x => !x.IsGenericPassenger)
            .Where(x => !string.IsNullOrWhiteSpace(x.DocumentNumber))
            .GroupBy(x => x.DocumentNumber.Trim().ToUpperInvariant())
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicatedDocuments.Any())
            throw new Exception($"Existen documentos duplicados: {string.Join(", ", duplicatedDocuments)}.");

        await Task.CompletedTask;
    }

    private async Task ValidateCapacityAsync(CreateReservationModel model, Guid? reservationIdToExclude)
    {
        if (!model.OutboundRouteScheduleId.HasValue || !model.OutboundDate.HasValue)
            throw new Exception("Debe seleccionar viaje y fecha de la reserva.");

        var requestedTrips = new List<(Guid ScheduleId, DateTime Date)>();

        foreach (var passenger in model.Passengers.Where(x => !x.IsCancelled))
        {
            if (passenger.Outbound)
                requestedTrips.Add((model.OutboundRouteScheduleId.Value, model.OutboundDate.Value.Date));

            if (passenger.Return)
                requestedTrips.Add((model.OutboundRouteScheduleId.Value, passenger.ReturnDate!.Value.Date));
        }

        var grouped = requestedTrips
            .GroupBy(x => new { x.ScheduleId, x.Date })
            .Select(x => new
            {
                x.Key.ScheduleId,
                x.Key.Date,
                Requested = x.Count()
            })
            .ToList();

        foreach (var group in grouped)
        {
            var schedule = await _repo.GetRouteScheduleAsync(group.ScheduleId);

            if (schedule is null)
                throw new Exception("Configuración de viaje no encontrada.");

            var locked = await _repo.IsBoatDailyTripLockedAsync(group.ScheduleId, group.Date);

            if (locked)
                throw new Exception($"El viaje del {group.Date:dd/MM/yyyy} ya tiene documento generado o está cerrado.");

            var reserved = await _repo.CountReservedPassengerTripsAsync(
                group.ScheduleId,
                group.Date,
                reservationIdToExclude);

            var capacity = schedule.Boat.Capacity + schedule.Boat.ExtraCapacity;
            var available = capacity - reserved;

            if (available < group.Requested)
                throw new Exception($"No existen cupos suficientes para el viaje del {group.Date:dd/MM/yyyy}. Disponibles: {available}, solicitados: {group.Requested}.");
        }
    }

    private async Task<Guid> GetRequiredCurrentUserIdAsync()
    {
        var user = await _currentUser.LoadAsync();

        if (user is null || user.UserId == Guid.Empty)
            throw new Exception("Usuario no autenticado.");

        var exists = await _repo.UserExistsAsync(user.UserId);

        if (!exists)
            throw new Exception("El usuario autenticado no existe en la tabla Users.");

        return user.UserId;
    }

    private static void AddPassengerTrips(
        Reservation reservation,
        ReservationPassengerModel passenger,
        Customer? customer,
        Guid boatRouteScheduleId,
        DateTime outboundDate)
    {
        if (passenger.Outbound)
        {
            reservation.PassengerTrips.Add(CreatePassengerTrip(
                reservation.TenantId,
                reservation,
                passenger,
                customer,
                boatRouteScheduleId,
                DateOnly.FromDateTime(outboundDate.Date),
                "Outbound"));
        }

        if (passenger.Return)
        {
            reservation.PassengerTrips.Add(CreatePassengerTrip(
                reservation.TenantId,
                reservation,
                passenger,
                customer,
                boatRouteScheduleId,
                DateOnly.FromDateTime(passenger.ReturnDate!.Value.Date),
                "Return"));
        }
    }

    private static ReservationPassengerTrip CreatePassengerTrip(
        Guid tenantId,
        Reservation reservation,
        ReservationPassengerModel passenger,
        Customer? customer,
        Guid boatRouteScheduleId,
        DateOnly travelDate,
        string segmentType)
    {
        return new ReservationPassengerTrip
        {
            TenantId = tenantId,
            Reservation = reservation,
            CustomerId = customer?.Id,
            GenericPassengerName = passenger.IsGenericPassenger ? passenger.FullName : null,
            GenericDocumentNumber = passenger.IsGenericPassenger ? passenger.DocumentNumber : null,
            BoatRouteScheduleId = boatRouteScheduleId,
            TravelDate = travelDate,
            SegmentType = segmentType,
            PassengerType = NormalizePassengerType(passenger.PassengerType),
            UnitPrice = 0,
            Discount = 0,
            TotalPrice = 0,
            Status = "Reserved",
            CreatedAt = DateTime.UtcNow
        };
    }

    private static int CountUniquePassengers(IEnumerable<ReservationPassengerTrip> trips)
    {
        return trips
            .Where(x => x.Status != "Cancelled")
            .GroupBy(GetPassengerGroupKey)
            .Count();
    }

    private static string GetPassengerGroupKey(ReservationPassengerTrip trip)
    {
        if (trip.CustomerId.HasValue)
            return $"C:{trip.CustomerId.Value}";

        return $"G:{trip.GenericDocumentNumber}|{trip.GenericPassengerName}|{trip.PassengerType}|{trip.Id}";
    }

    private static string GetPassengerDocument(ReservationPassengerTrip trip)
    {
        return trip.Customer?.DocumentNumber
               ?? trip.GenericDocumentNumber
               ?? string.Empty;
    }

    private static string GetPassengerName(ReservationPassengerTrip trip)
    {
        return trip.Customer?.FullName
               ?? trip.GenericPassengerName
               ?? "Pasajero genérico";
    }

    private static object ToAuditTrip(ReservationPassengerTrip x)
    {
        return new
        {
            x.Id,
            x.ReservationId,
            x.CustomerId,
            x.GenericPassengerName,
            x.GenericDocumentNumber,
            x.BoatRouteScheduleId,
            x.TravelDate,
            x.SegmentType,
            x.PassengerType,
            x.Status,
            x.UnitPrice,
            x.Discount,
            x.TotalPrice
        };
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

    private static void NormalizeGenericPassenger(ReservationPassengerModel passenger)
    {
        if (!passenger.IsGenericPassenger)
            return;

        passenger.DocumentType = "Generico";

        if (string.IsNullOrWhiteSpace(passenger.DocumentNumber))
            passenger.DocumentNumber = GenerateGenericPassengerDocument();

        if (string.IsNullOrWhiteSpace(passenger.FullName))
            passenger.FullName = "Pasajero genérico";

        if (string.IsNullOrWhiteSpace(passenger.Nationality))
            passenger.Nationality = "Ecuatoriana";

        passenger.Age ??= 18;

        if (string.IsNullOrWhiteSpace(passenger.PassengerType))
            passenger.PassengerType = "Adult";

        passenger.IsNewCustomer = true;
        passenger.CustomerChanged = true;
    }

    private static string GenerateGenericPassengerDocument()
    {
        return $"GEN-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    }
}