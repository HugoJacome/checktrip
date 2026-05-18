using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Features.Reservations.Models.Operations;
using CheckTrip.Web.Features.Tickets.Models;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Infrastructure.Repositories;

public class BoatDailyTripRepository : BaseRepository<BoatDailyTrip>
{
    private readonly ITenantProvider _tenant;

    public BoatDailyTripRepository(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantProvider tenant)
        : base(dbFactory)
    {
        _tenant = tenant;
    }

    public Task<Guid> GetTenantIdAsync()
    {
        return Task.FromResult(_tenant.GetTenantId());
    }

    public async Task<List<RouteScheduleCatalogItem>> GetRouteSchedulesByBoatAsync(Guid boatId)
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
                x.BoatId == boatId)
            .OrderBy(x => x.Route.Origin)
            .ThenBy(x => x.Route.Destination)
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

    public async Task<BoatRouteSchedule?> GetRouteScheduleDetailAsync(Guid boatRouteScheduleId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
            .AsNoTracking()
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Id == boatRouteScheduleId &&
                x.IsActive);
    }

    public async Task<BoatRouteSchedule?> GetFirstScheduleByBoatAsync(Guid boatId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
            .AsNoTracking()
            .Include(x => x.Schedule)
            .Where(x =>
                x.TenantId == tenantId &&
                x.BoatId == boatId &&
                x.IsActive)
            .OrderBy(x => x.Schedule.DepartureTime)
            .FirstOrDefaultAsync();
    }

    public async Task<BoatDailyTrip?> GetByScheduleAndDateAsync(Guid boatRouteScheduleId, DateOnly tripDate)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatDailyTrips
            .AsNoTracking()
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .Include(x => x.Crew)
            .Include(x => x.Comments)
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.BoatRouteScheduleId == boatRouteScheduleId &&
                x.TripDate == tripDate);
    }

    public async Task<BoatDailyTrip?> GetByIdAsync(Guid boatDailyTripId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatDailyTrips
            .AsNoTracking()
            .Include(x => x.BoatRouteSchedule)
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .Include(x => x.Crew)
            .Include(x => x.Comments)
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Id == boatDailyTripId);
    }

    public async Task<BoatDailyTrip?> GetForUpdateAsync(Guid boatDailyTripId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatDailyTrips
            .Include(x => x.Crew)
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Id == boatDailyTripId);
    }

    public async Task<BoatDailyTrip?> GetForUpdateByScheduleAndDateAsync(Guid boatRouteScheduleId, DateOnly tripDate)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatDailyTrips
            .Include(x => x.Crew)
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.BoatRouteScheduleId == boatRouteScheduleId &&
                x.TripDate == tripDate);
    }

    public async Task<bool> IsLockedAsync(Guid boatRouteScheduleId, DateOnly tripDate)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatDailyTrips.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.BoatRouteScheduleId == boatRouteScheduleId &&
            x.TripDate == tripDate &&
            (
                x.Status == BoatDailyTripStatus.DocumentGenerated ||
                x.Status == BoatDailyTripStatus.Closed
            ));
    }

    public async Task<BoatDailyTrip> CreateAsync(BoatDailyTrip trip)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        db.BoatDailyTrips.Add(trip);
        await db.SaveChangesAsync();

        return trip;
    }

    public async Task UpdateAsync(BoatDailyTrip trip)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        db.BoatDailyTrips.Update(trip);
        await db.SaveChangesAsync();
    }

    public async Task<BoatDailyTrip> GetOrCreateAsync(BoatRouteSchedule schedule, DateOnly tripDate)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var trip = await db.BoatDailyTrips
            .Include(x => x.Crew)
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.BoatRouteScheduleId == schedule.Id &&
                x.TripDate == tripDate);

        if (trip is not null)
            return trip;

        trip = new BoatDailyTrip
        {
            TenantId = tenantId,
            BoatRouteScheduleId = schedule.Id,
            BoatId = schedule.BoatId,
            RouteId = schedule.RouteId,
            ScheduleId = schedule.ScheduleId,
            TripDate = tripDate,
            Status = BoatDailyTripStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        db.BoatDailyTrips.Add(trip);
        await db.SaveChangesAsync();

        return trip;
    }

    public async Task<BoatDailyTrip> GenerateDocumentAsync(
        BoatRouteSchedule schedule,
        DateOnly tripDate,
        Guid userId,
        string documentNumber)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var trip = await db.BoatDailyTrips.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId &&
            x.BoatRouteScheduleId == schedule.Id &&
            x.TripDate == tripDate);

        if (trip is null)
        {
            trip = new BoatDailyTrip
            {
                TenantId = tenantId,
                BoatRouteScheduleId = schedule.Id,
                BoatId = schedule.BoatId,
                RouteId = schedule.RouteId,
                ScheduleId = schedule.ScheduleId,
                TripDate = tripDate,
                Status = BoatDailyTripStatus.DocumentGenerated,
                DocumentGeneratedAt = DateTime.UtcNow,
                DocumentGeneratedByUserId = userId,
                DocumentNumber = documentNumber,
                CreatedAt = DateTime.UtcNow
            };

            db.BoatDailyTrips.Add(trip);
        }
        else
        {
            if (trip.Status == BoatDailyTripStatus.Closed)
                throw new Exception("El viaje ya está cerrado.");

            trip.Status = BoatDailyTripStatus.DocumentGenerated;
            trip.DocumentGeneratedAt = DateTime.UtcNow;
            trip.DocumentGeneratedByUserId = userId;
            trip.DocumentNumber ??= documentNumber;
            trip.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return trip;
    }

    public async Task<BoatDailyTrip> CloseAsync(Guid boatDailyTripId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var trip = await db.BoatDailyTrips.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId &&
            x.Id == boatDailyTripId);

        if (trip is null)
            throw new Exception("Viaje no encontrado.");

        if (trip.Status == BoatDailyTripStatus.Closed)
            throw new Exception("El viaje ya está cerrado.");

        if (trip.Status != BoatDailyTripStatus.DocumentGenerated)
            throw new Exception("Primero debe generar el documento del viaje.");

        trip.Status = BoatDailyTripStatus.Closed;
        trip.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return trip;
    }

    public async Task<BoatDailyTrip> CloseByScheduleAndDateAsync(Guid boatRouteScheduleId, DateOnly tripDate)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var trip = await db.BoatDailyTrips.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId &&
            x.BoatRouteScheduleId == boatRouteScheduleId &&
            x.TripDate == tripDate);

        if (trip is null)
            throw new Exception("Primero debe generar el documento del viaje.");

        if (trip.Status == BoatDailyTripStatus.Closed)
            throw new Exception("El viaje ya está cerrado.");

        if (trip.Status != BoatDailyTripStatus.DocumentGenerated)
            throw new Exception("Primero debe generar el documento del viaje.");

        trip.Status = BoatDailyTripStatus.Closed;
        trip.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return trip;
    }

    public async Task SaveCrewAsync(Guid boatDailyTripId, BoatDailyTripCrew crew)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var trip = await db.BoatDailyTrips
            .Include(x => x.Crew)
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Id == boatDailyTripId);

        if (trip is null)
            throw new Exception("Viaje no encontrado.");

        if (trip.Status == BoatDailyTripStatus.DocumentGenerated ||
            trip.Status == BoatDailyTripStatus.Closed)
            throw new Exception("El viaje ya tiene documento generado o está cerrado.");

        if (trip.Crew is null)
        {
            crew.TenantId = tenantId;
            crew.BoatDailyTripId = boatDailyTripId;
            crew.CreatedAt = DateTime.UtcNow;

            db.BoatDailyTripCrews.Add(crew);
        }
        else
        {
            trip.Crew.CaptainName = crew.CaptainName;
            trip.Crew.CaptainDocument = crew.CaptainDocument;
            trip.Crew.Sailor1Name = crew.Sailor1Name;
            trip.Crew.Sailor1Document = crew.Sailor1Document;
            trip.Crew.Sailor2Name = crew.Sailor2Name;
            trip.Crew.Sailor2Document = crew.Sailor2Document;
            trip.Crew.Sailor3Name = crew.Sailor3Name;
            trip.Crew.Sailor3Document = crew.Sailor3Document;
            trip.Crew.Notes = crew.Notes;
            trip.Crew.UpdatedAt = DateTime.UtcNow;
        }

        trip.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    public async Task AddCommentAsync(BoatDailyTripComment comment)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var tripExists = await db.BoatDailyTrips.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.Id == comment.BoatDailyTripId);

        if (!tripExists)
            throw new Exception("Viaje no encontrado.");

        comment.TenantId = tenantId;
        comment.CreatedAt = DateTime.UtcNow;

        db.BoatDailyTripComments.Add(comment);
        await db.SaveChangesAsync();
    }
    public async Task<List<BoatDailyTrip>> GetOperationsAsync(BoatDailyTripOperationsFilter filter)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.BoatDailyTrips
            .AsNoTracking()
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .Include(x => x.Crew)
            .Include(x => x.Comments)
            .Where(x => x.TenantId == tenantId);

        if (filter.DateFrom.HasValue)
        {
            var date = DateOnly.FromDateTime(filter.DateFrom.Value.Date);
            query = query.Where(x => x.TripDate >= date);
        }

        if (filter.DateTo.HasValue)
        {
            var date = DateOnly.FromDateTime(filter.DateTo.Value.Date);
            query = query.Where(x => x.TripDate <= date);
        }

        if (filter.BoatId.HasValue)
            query = query.Where(x => x.BoatId == filter.BoatId.Value);

        if (filter.RouteId.HasValue)
            query = query.Where(x => x.RouteId == filter.RouteId.Value);

        if (filter.ScheduleId.HasValue)
            query = query.Where(x => x.ScheduleId == filter.ScheduleId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => x.Status == filter.Status);

        if (filter.DocumentGenerated.HasValue)
        {
            if (filter.DocumentGenerated.Value)
                query = query.Where(x => !string.IsNullOrWhiteSpace(x.DocumentNumber));
            else
                query = query.Where(x => string.IsNullOrWhiteSpace(x.DocumentNumber));
        }

        if (filter.AgencyId.HasValue)
        {
            query = query.Where(x => db.ReservationPassengerTrips.Any(p =>
                p.TenantId == tenantId &&
                p.BoatRouteScheduleId == x.BoatRouteScheduleId &&
                p.TravelDate == x.TripDate &&
                p.Status != "Cancelled" &&
                p.Reservation.AgencyId == filter.AgencyId.Value));
        }

        if (filter.SellerId.HasValue)
        {
            query = query.Where(x => db.ReservationPassengerTrips.Any(p =>
                p.TenantId == tenantId &&
                p.BoatRouteScheduleId == x.BoatRouteScheduleId &&
                p.TravelDate == x.TripDate &&
                p.Status != "Cancelled" &&
                p.Reservation.SellerId == filter.SellerId.Value));
        }

        return await query
            .OrderByDescending(x => x.TripDate)
            .ThenBy(x => x.Boat.Name)
            .ThenBy(x => x.Schedule.DepartureTime)
            .ToListAsync();
    }

    public async Task<List<ReservationPassengerTrip>> GetPassengerTripsByBoatDailyTripAsync(Guid boatDailyTripId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var trip = await db.BoatDailyTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Id == boatDailyTripId);

        if (trip is null)
            return [];

        return await db.ReservationPassengerTrips
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
            .Where(x =>
                x.TenantId == tenantId &&
                x.Status != "Cancelled" &&
                x.Reservation.Status != "Cancelled" &&
                x.BoatRouteScheduleId == trip.BoatRouteScheduleId &&
                x.TravelDate == trip.TripDate)
            .OrderBy(x => x.Reservation.ReservationCode)
            .ThenBy(x => x.Customer != null ? x.Customer.FullName : x.GenericPassengerName)
            .ToListAsync();
    }

    public async Task<int> CountPassengerTripsByBoatDailyTripAsync(Guid boatDailyTripId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var trip = await db.BoatDailyTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Id == boatDailyTripId);

        if (trip is null)
            return 0;

        return await db.ReservationPassengerTrips
            .AsNoTracking()
            .CountAsync(x =>
                x.TenantId == tenantId &&
                x.Status != "Cancelled" &&
                x.Reservation.Status != "Cancelled" &&
                x.BoatRouteScheduleId == trip.BoatRouteScheduleId &&
                x.TravelDate == trip.TripDate);
    }

    public async Task<List<ReservationPassengerTrip>> GetPassengerTripsForOperationsAsync(
        BoatDailyTripOperationsFilter filter)
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
            .Where(x =>
                x.TenantId == tenantId &&
                x.Status != "Cancelled" &&
                x.Reservation.Status != "Cancelled");

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

        if (filter.BoatId.HasValue)
            query = query.Where(x => x.BoatRouteSchedule.BoatId == filter.BoatId.Value);

        if (filter.RouteId.HasValue)
            query = query.Where(x => x.BoatRouteSchedule.RouteId == filter.RouteId.Value);

        if (filter.ScheduleId.HasValue)
            query = query.Where(x => x.BoatRouteSchedule.ScheduleId == filter.ScheduleId.Value);

        if (filter.AgencyId.HasValue)
            query = query.Where(x => x.Reservation.AgencyId == filter.AgencyId.Value);

        if (filter.SellerId.HasValue)
            query = query.Where(x => x.Reservation.SellerId == filter.SellerId.Value);

        return await query
            .OrderByDescending(x => x.TravelDate)
            .ThenBy(x => x.Reservation.ReservationCode)
            .ThenBy(x => x.Customer != null ? x.Customer.FullName : x.GenericPassengerName)
            .ToListAsync();
    }
    public async Task<BoatDailyTrip> ChangeBoatRouteScheduleAsync(
    Guid boatDailyTripId,
    BoatRouteSchedule newSchedule)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var trip = await db.BoatDailyTrips.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId &&
            x.Id == boatDailyTripId);

        if (trip is null)
            throw new Exception("Viaje no encontrado.");

        if (trip.Status == BoatDailyTripStatus.DocumentGenerated ||
            trip.Status == BoatDailyTripStatus.Closed)
            throw new Exception("El viaje ya tiene documento generado o está cerrado.");

        trip.BoatRouteScheduleId = newSchedule.Id;
        trip.BoatId = newSchedule.BoatId;
        trip.RouteId = newSchedule.RouteId;
        trip.ScheduleId = newSchedule.ScheduleId;
        trip.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return trip;
    }
    public async Task<List<RouteScheduleCatalogItem>> GetAllRouteSchedulesAsync()
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
            .ThenBy(x => x.Route.Destination)
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