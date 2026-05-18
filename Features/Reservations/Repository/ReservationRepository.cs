using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Features.Reservations.Models;
using CheckTrip.Web.Features.Reservations.Models.Operations;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Infrastructure.Repositories;

public class ReservationRepository : BaseRepository<Reservation>
{
    private readonly ITenantProvider _tenant;

    public ReservationRepository(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantProvider tenant)
        : base(dbFactory)
    {
        _tenant = tenant;
    }

    public async Task<List<Reservation>> GetMonitorAsync()
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Reservations
            .AsNoTracking()
            .Include(x => x.Agency)
            .Include(x => x.Seller)
            .Include(x => x.PassengerTrips)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Reservation?> GetFullAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Reservations
            .Include(x => x.PassengerTrips)
                .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<Reservation?> GetDetailAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Reservations
            .AsNoTracking()
            .Include(x => x.Agency)
            .Include(x => x.Seller)
            .Include(x => x.PassengerTrips)
                .ThenInclude(x => x.Customer)
            .Include(x => x.PassengerTrips)
                .ThenInclude(x => x.BoatRouteSchedule)
                    .ThenInclude(x => x.Boat)
            .Include(x => x.PassengerTrips)
                .ThenInclude(x => x.BoatRouteSchedule)
                    .ThenInclude(x => x.Route)
            .Include(x => x.PassengerTrips)
                .ThenInclude(x => x.BoatRouteSchedule)
                    .ThenInclude(x => x.Schedule)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<BoatRouteSchedule?> GetRouteScheduleAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
            .AsNoTracking()
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId &&
                x.IsActive);
    }

    public async Task<Customer?> GetCustomerByDocumentAsync(string documentNumber)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.DocumentNumber == documentNumber);
    }

    public async Task<List<Customer>> SearchCustomersAsync(string text)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        text = text.Trim().ToLower();

        return await db.Customers
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.IsActive &&
                (
                    x.DocumentNumber.ToLower().Contains(text) ||
                    x.FullName.ToLower().Contains(text)
                ))
            .OrderBy(x => x.FullName)
            .Take(10)
            .ToListAsync();
    }

    public async Task<List<BoatRouteSchedule>> GetRouteSchedulesByRouteAsync(Guid? routeId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.BoatRouteSchedules
            .AsNoTracking()
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .Where(x => x.TenantId == tenantId && x.IsActive);

        if (routeId.HasValue)
            query = query.Where(x => x.RouteId == routeId.Value);

        return await query
            .OrderBy(x => x.Schedule.Name)
            .ThenBy(x => x.Boat.Name)
            .ToListAsync();
    }

    public async Task<List<TripRoute>> GetRoutesByBoatAsync(Guid boatId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
            .AsNoTracking()
            .Include(x => x.Route)
            .Where(x =>
                x.TenantId == tenantId &&
                x.IsActive &&
                x.BoatId == boatId &&
                x.Route != null)
            .Select(x => x.Route)
            .Distinct()
            .OrderBy(x => x.Origin)
            .ThenBy(x => x.Destination)
            .ToListAsync();
    }

    public async Task<List<BoatRouteSchedule>> GetRouteSchedulesAsync(Guid boatId, Guid routeId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
            .AsNoTracking()
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .Where(x =>
                x.TenantId == tenantId &&
                x.IsActive &&
                x.BoatId == boatId &&
                x.RouteId == routeId)
            .OrderBy(x => x.Schedule.Name)
            .ToListAsync();
    }

    public async Task<List<Boat>> GetBoatsByRouteAsync(Guid routeId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
            .AsNoTracking()
            .Include(x => x.Boat)
            .Where(x =>
                x.TenantId == tenantId &&
                x.IsActive &&
                x.RouteId == routeId &&
                x.Boat != null)
            .Select(x => x.Boat)
            .Distinct()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<int> CountReservedPassengerTripsAsync(
        Guid routeScheduleId,
        DateTime date,
        Guid? reservationIdToExclude)
    {
        var tenantId = _tenant.GetTenantId();
        var travelDate = DateOnly.FromDateTime(date.Date);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.ReservationPassengerTrips
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.BoatRouteScheduleId == routeScheduleId &&
                x.TravelDate == travelDate &&
                x.Status != "Cancelled");

        if (reservationIdToExclude.HasValue)
            query = query.Where(x => x.ReservationId != reservationIdToExclude.Value);

        return await query.CountAsync();
    }

    public async Task<bool> IsBoatDailyTripLockedAsync(Guid boatRouteScheduleId, DateTime date)
    {
        var tenantId = _tenant.GetTenantId();
        var tripDate = DateOnly.FromDateTime(date.Date);

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatDailyTrips.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.BoatRouteScheduleId == boatRouteScheduleId &&
            x.TripDate == tripDate &&
            (x.Status == "DocumentGenerated" || x.Status == "Closed"));
    }

    public async Task<bool> UserExistsAsync(Guid userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Users.AnyAsync(x => x.Id == userId);
    }

    public async Task<string> GenerateReservationCodeAsync()
    {
        var tenantId = _tenant.GetTenantId();
        var prefix = $"RSV-{DateTime.UtcNow:yyyyMMdd}";

        await using var db = await _dbFactory.CreateDbContextAsync();

        var lastCode = await db.Reservations
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.ReservationCode.StartsWith(prefix))
            .OrderByDescending(x => x.ReservationCode)
            .Select(x => x.ReservationCode)
            .FirstOrDefaultAsync();

        var next = 1;

        if (!string.IsNullOrWhiteSpace(lastCode))
        {
            var lastPart = lastCode.Split('-').LastOrDefault();

            if (int.TryParse(lastPart, out var lastNumber))
                next = lastNumber + 1;
        }

        string code;

        do
        {
            code = $"{prefix}-{next:0000}";
            next++;
        }
        while (await db.Reservations.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.ReservationCode == code));

        return code;
    }

    public async Task<Guid> CreateReservationAsync(Reservation reservation, ReservationHistory history)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();

            db.Reservations.Add(reservation);
            db.ReservationHistory.Add(history);

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return reservation.Id;
        });
    }

    public async Task<Reservation?> GetForUpdateAsync(Guid reservationId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Reservations
            .Include(x => x.PassengerTrips)
            .FirstOrDefaultAsync(x => x.Id == reservationId && x.TenantId == tenantId);
    }

    public async Task SaveReservationUpdateAsync(Reservation reservation, ReservationHistory history)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        db.Reservations.Update(reservation);
        db.ReservationHistory.Add(history);

        await db.SaveChangesAsync();
    }

    public async Task<Customer?> GetOrCreateCustomerAsync(ReservationPassengerModel passenger)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var document = passenger.DocumentNumber.Trim();
        var fullName = string.IsNullOrWhiteSpace(passenger.FullName)
            ? "Pasajero"
            : passenger.FullName.Trim();

        var customer = await db.Customers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.DocumentNumber == document);

        if (customer is not null)
        {
            customer.DocumentType = passenger.DocumentType;
            customer.FullName = fullName;
            customer.Nationality = passenger.Nationality;
            customer.Age = passenger.Age;
            customer.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return customer;
        }

        customer = new Customer
        {
            TenantId = tenantId,
            DocumentType = passenger.DocumentType,
            DocumentNumber = document,
            FullName = fullName,
            Nationality = passenger.Nationality,
            Age = passenger.Age,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return customer;
    }

    public async Task<Reservation?> GetReservationWithPassengerTripsAsync(Guid reservationId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Reservations
            .Include(x => x.PassengerTrips)
                .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == reservationId && x.TenantId == tenantId);
    }

    public async Task SaveReservationWithHistoryAsync(Reservation reservation, ReservationHistory history)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        db.Reservations.Update(reservation);
        db.ReservationHistory.Add(history);

        await db.SaveChangesAsync();
    }

    public async Task<List<Reservation>> GetOperationsReservationsRawAsync(ReservationOperationsFilter filter)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.Reservations
            .AsNoTracking()
            .Include(x => x.Agency)
            .Include(x => x.Seller)
            .Include(x => x.Comments)
            .Include(x => x.PassengerTrips)
                .ThenInclude(x => x.BoatRouteSchedule)
                    .ThenInclude(x => x.Boat)
            .Include(x => x.PassengerTrips)
                .ThenInclude(x => x.BoatRouteSchedule)
                    .ThenInclude(x => x.Route)
            .Include(x => x.PassengerTrips)
                .ThenInclude(x => x.BoatRouteSchedule)
                    .ThenInclude(x => x.Schedule)
            .Where(x => x.TenantId == tenantId);

        if (filter.DateFrom.HasValue)
        {
            var date = DateOnly.FromDateTime(filter.DateFrom.Value.Date);
            query = query.Where(x => x.PassengerTrips.Any(i => i.TravelDate >= date));
        }

        if (filter.DateTo.HasValue)
        {
            var date = DateOnly.FromDateTime(filter.DateTo.Value.Date);
            query = query.Where(x => x.PassengerTrips.Any(i => i.TravelDate <= date));
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
            query = query.Where(x => x.PassengerTrips.Any(i => i.BoatRouteSchedule.BoatId == filter.BoatId.Value));

        if (filter.RouteId.HasValue)
            query = query.Where(x => x.PassengerTrips.Any(i => i.BoatRouteSchedule.RouteId == filter.RouteId.Value));

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<ReservationPassengerTrip>> GetOperationsPassengersRawAsync(ReservationOperationsFilter filter)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.ReservationPassengerTrips
            .AsNoTracking()
            .Include(x => x.Reservation)
                .ThenInclude(x => x.Agency)
            .Include(x => x.Reservation)
                .ThenInclude(x => x.Seller)
            .Include(x => x.Customer)
            .Include(x => x.BoatRouteSchedule)
                .ThenInclude(x => x.Boat)
            .Include(x => x.BoatRouteSchedule)
                .ThenInclude(x => x.Route)
            .Include(x => x.BoatRouteSchedule)
                .ThenInclude(x => x.Schedule)
            .Where(x => x.TenantId == tenantId);

        if (filter.DateFrom.HasValue)
        {
            var date = DateOnly.FromDateTime(filter.DateFrom.Value.Date);
            query = query.Where(x => x.TravelDate >= date);
        }

        if (filter.DateTo.HasValue)
        {
            var date = DateOnly.FromDateTime(filter.DateTo.Value.Date);
            query = query.Where(x => x.TravelDate <= date);
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
            query = query.Where(x => x.BoatRouteSchedule.BoatId == filter.BoatId.Value);

        if (filter.RouteId.HasValue)
            query = query.Where(x => x.BoatRouteSchedule.RouteId == filter.RouteId.Value);

        return await query
            .OrderByDescending(x => x.TravelDate)
            .ThenBy(x => x.Reservation.ReservationCode)
            .ThenBy(x => x.Customer != null ? x.Customer.FullName : x.GenericPassengerName)
            .ToListAsync();
    }

    public async Task<Reservation?> GetReservationForCommentAsync(Guid reservationId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Reservations
            .FirstOrDefaultAsync(x => x.Id == reservationId && x.TenantId == tenantId);
    }

    public async Task AddCommentAsync(Reservation reservation, ReservationComment comment, ReservationHistory history)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        db.Reservations.Update(reservation);
        db.ReservationComments.Add(comment);
        db.ReservationHistory.Add(history);

        await db.SaveChangesAsync();
    }

    public async Task<BoatRouteSchedule?> GetActiveRouteScheduleWithDetailAsync(Guid routeScheduleId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .FirstOrDefaultAsync(x =>
                x.Id == routeScheduleId &&
                x.TenantId == tenantId &&
                x.IsActive);
    }

    public async Task<Reservation?> GetReservationForBoatChangeAsync(Guid reservationId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Reservations
            .Include(x => x.PassengerTrips)
                .ThenInclude(x => x.BoatRouteSchedule)
                    .ThenInclude(x => x.Boat)
            .FirstOrDefaultAsync(x => x.Id == reservationId && x.TenantId == tenantId);
    }

    public async Task SaveBoatChangeAsync(Reservation reservation, ReservationHistory history)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        db.Reservations.Update(reservation);
        db.ReservationHistory.Add(history);

        await db.SaveChangesAsync();
    }

    public async Task<List<CatalogItem>> GetOperationBoatsAsync()
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Boats
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

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Agencies
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

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Sellers
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

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
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
}