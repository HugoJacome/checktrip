using CheckTrip.Web.Data;
using CheckTrip.Web.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

public class AgencyRepository : BaseRepository<Agency>
{
    private readonly ITenantProvider _tenant;

    public AgencyRepository(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantProvider tenant)
        : base(dbFactory)
    {
        _tenant = tenant;
    }

    public async Task<List<Agency>> GetAllAsync()
    {
        var tenantId = _tenant.GetTenantId();
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Agencies
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<Agency?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Agencies
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null)
    {
        var tenantId = _tenant.GetTenantId();
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Agencies.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.IsActive &&
            x.Name == name &&
            (!excludeId.HasValue || x.Id != excludeId.Value));
    }
}