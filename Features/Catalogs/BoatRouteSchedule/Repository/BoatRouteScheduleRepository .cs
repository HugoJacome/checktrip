using CheckTrip.Web.Data;
using CheckTrip.Web.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

public class BoatRouteScheduleRepository : BaseRepository<BoatRouteSchedule>
{
    private readonly ITenantProvider _tenant;

    public BoatRouteScheduleRepository(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantProvider tenant)
        : base(dbFactory)
    {
        _tenant = tenant;
    }

    public async Task<List<BoatRouteSchedule>> GetAllAsync()
    {
        var tenantId = _tenant.GetTenantId();
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<BoatRouteSchedule?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<bool> ExistsAsync(Guid boatId, Guid routeId, Guid scheduleId)
    {
        var tenantId = _tenant.GetTenantId();
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.BoatId == boatId &&
            x.RouteId == routeId &&
            x.ScheduleId == scheduleId &&
            x.IsActive);
    }
}