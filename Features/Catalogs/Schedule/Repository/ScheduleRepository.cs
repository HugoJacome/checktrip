using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Infrastructure.Repositories;

public class ScheduleRepository : BaseRepository<Schedule>
{
    private readonly ITenantProvider _tenant;

    public ScheduleRepository(AppDbContext db, ITenantProvider tenant)
        : base(db)
    {
        _tenant = tenant;
    }

    public async Task<List<Schedule>> GetAllAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Schedules
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.DepartureTime)
            .ToListAsync();
    }

    public async Task<Schedule?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Schedules
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }
}