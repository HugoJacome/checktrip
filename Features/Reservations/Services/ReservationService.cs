using CheckAccess.Infrastructure.Auth;
using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Features.Reservations.Models;
using CheckTrip.Web.Infrastructure.Audit;
using CheckTrip.Web.Infrastructure.Auth;
using CheckTrip.Web.Infrastructure.Repositories;
using CheckTrip.Web.Infrastructure.Tenant;

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

    public async Task<Guid> CreateAsync(CreateReservationModel model)
    {
        await ValidateAsync(model);

        var tenantId = _tenant.GetTenantId();
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var outboundPassengers = model.Passengers.Count(x => x.Outbound);
        var returnPassengers = model.Passengers.Count(x => x.Return);

        if (model.OutboundRouteScheduleId.HasValue && model.OutboundDate.HasValue)
        {
            var outboundAvailability = await GetAvailabilityAsync(
                model.OutboundRouteScheduleId.Value,
                model.OutboundDate.Value,
                outbound: true);

            if (outboundAvailability.Available < outboundPassengers)
                throw new Exception("No existen cupos suficientes para la ida.");
        }

        if (model.ReturnRouteScheduleId.HasValue && model.ReturnDate.HasValue)
        {
            var returnAvailability = await GetAvailabilityAsync(
                model.ReturnRouteScheduleId.Value,
                model.ReturnDate.Value,
                outbound: false);

            if (returnAvailability.Available < returnPassengers)
                throw new Exception("No existen cupos suficientes para el retorno.");
        }

        using var tx = await _db.Database.BeginTransactionAsync();

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

            var tripType = GetTripType(passenger);

            var item = new ReservationItem
            {
                TenantId = tenantId,
                Reservation = reservation,
                CustomerId = customer.Id,
                TripType = tripType,
                PassengerType = passenger.PassengerType,

                OutboundRouteScheduleId = passenger.Outbound ? model.OutboundRouteScheduleId : null,
                OutboundTravelDate = passenger.Outbound && model.OutboundDate.HasValue
                    ? DateOnly.FromDateTime(model.OutboundDate.Value)
                    : null,

                ReturnRouteScheduleId = passenger.Return ? model.ReturnRouteScheduleId : null,
                ReturnTravelDate = passenger.Return && model.ReturnDate.HasValue
                    ? DateOnly.FromDateTime(model.ReturnDate.Value)
                    : null,

                Status = "Reserved",
                CreatedAt = DateTime.UtcNow
            };

            reservation.Items.Add(item);
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

        await _audit.LogAsync("Reservation", "Create", null, reservation);

        return reservation.Id;
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

        await _audit.LogAsync("Reservation", "Cancel", new { oldStatus }, reservation);
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

        await _audit.LogAsync("Reservation", "Finish", new { oldStatus }, reservation);
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

        if (model.Passengers.Any(x => x.Outbound) &&
            (!model.OutboundRouteScheduleId.HasValue || !model.OutboundDate.HasValue))
            throw new Exception("Debe seleccionar viaje y fecha de ida.");

        if (model.Passengers.Any(x => x.Return) &&
            (!model.ReturnRouteScheduleId.HasValue || !model.ReturnDate.HasValue))
            throw new Exception("Debe seleccionar viaje y fecha de retorno.");

        await Task.CompletedTask;
    }

    private async Task<Customer> GetOrCreateCustomerAsync(
        ReservationPassengerModel passenger,
        Guid tenantId)
    {
        var customer = await _repo.GetCustomerByDocumentAsync(passenger.DocumentNumber.Trim());

        if (customer is not null)
        {
            customer.FullName = passenger.FullName.Trim();
            customer.Age = passenger.Age;
            return customer;
        }

        customer = new Customer
        {
            TenantId = tenantId,
            DocumentType = passenger.DocumentType,
            DocumentNumber = passenger.DocumentNumber.Trim(),
            FullName = passenger.FullName.Trim(),
            Age = passenger.Age,
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

        return "Return";
    }

    private async Task<string> GenerateReservationCodeAsync()
    {
        var count = await _repo.CountReservationsTodayAsync() + 1;
        return $"RSV-{DateTime.UtcNow:yyyyMMdd}-{count:0000}";
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
            Age = customer.Age
        };
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

        await _audit.LogAsync("Reservation", "ChangePaymentStatus", new { PaymentStatus = old }, new { PaymentStatus = paymentStatus });
    }
}