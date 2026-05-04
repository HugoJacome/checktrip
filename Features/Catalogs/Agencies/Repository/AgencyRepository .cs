using CheckTrip.Web.Data;
using CheckTrip.Web.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

public class AgencyRepository : BaseRepository<Agency>
{
    private readonly ITenantProvider _tenant;

    public AgencyRepository(AppDbContext db, ITenantProvider tenant)
        : base(db)
    {
        _tenant = tenant;
    }

    public async Task<List<Agency>> GetAllAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Agencies
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<Agency?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Agencies
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Agencies.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.IsActive &&
            x.Name == name &&
            (!excludeId.HasValue || x.Id != excludeId.Value));
    }
}