using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
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
            .Include(x => x.Items)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Reservation?> GetFullAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Reservations
            .Include(x => x.Items)
                .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<int> CountReservedAsync(Guid routeScheduleId, DateTime date, bool outbound)
    {
        var tenantId = _tenant.GetTenantId();
        var travelDate = DateOnly.FromDateTime(date);

        await using var db = await _dbFactory.CreateDbContextAsync();

        if (outbound)
        {
            return await db.ReservationItems.CountAsync(x =>
                x.TenantId == tenantId &&
                x.OutboundRouteScheduleId == routeScheduleId &&
                x.OutboundTravelDate == travelDate &&
                x.Status != "Cancelled");
        }

        return await db.ReservationItems.CountAsync(x =>
            x.TenantId == tenantId &&
            x.ReturnRouteScheduleId == routeScheduleId &&
            x.ReturnTravelDate == travelDate &&
            x.Status != "Cancelled");
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
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.DocumentNumber == documentNumber);
    }

    public async Task<int> CountReservationsTodayAsync()
    {
        var tenantId = _tenant.GetTenantId();
        var today = DateTime.UtcNow.Date;

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Reservations.CountAsync(x =>
            x.TenantId == tenantId &&
            x.CreatedAt >= today &&
            x.CreatedAt < today.AddDays(1));
    }

    public async Task<Reservation?> GetDetailAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Reservations
            .AsNoTracking()
            .Include(x => x.Agency)
            .Include(x => x.Seller)
            .Include(x => x.Items)
                .ThenInclude(x => x.Customer)
            .Include(x => x.Items)
                .ThenInclude(x => x.OutboundRouteSchedule)
                    .ThenInclude(x => x!.Route)
            .Include(x => x.Items)
                .ThenInclude(x => x.OutboundRouteSchedule)
                    .ThenInclude(x => x!.Schedule)
            .Include(x => x.Items)
                .ThenInclude(x => x.ReturnRouteSchedule)
                    .ThenInclude(x => x!.Route)
            .Include(x => x.Items)
                .ThenInclude(x => x.ReturnRouteSchedule)
                    .ThenInclude(x => x!.Schedule)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
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
}