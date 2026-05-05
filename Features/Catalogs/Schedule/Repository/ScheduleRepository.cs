using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Infrastructure.Repositories;

public class ScheduleRepository : BaseRepository<Schedule>
{
    private readonly ITenantProvider _tenant;

    public ScheduleRepository(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantProvider tenant)
        : base(dbFactory)
    {
        _tenant = tenant;
    }

    public async Task<List<Schedule>> GetAllAsync()
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Schedules
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.DepartureTime)
            .ToListAsync();
    }

    public async Task<Schedule?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Schedules
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }
}