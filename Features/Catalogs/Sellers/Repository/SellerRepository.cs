using CheckTrip.Web.Data;
using CheckTrip.Web.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

public class SellerRepository : BaseRepository<Seller>
{
    private readonly ITenantProvider _tenant;

    public SellerRepository(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantProvider tenant)
        : base(dbFactory)
    {
        _tenant = tenant;
    }

    public async Task<List<Seller>> GetAllAsync()
    {
        var tenantId = _tenant.GetTenantId();
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Sellers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ToListAsync();
    }

    public async Task<Seller?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Sellers
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<bool> ExistsByNameAsync(string firstName, string? lastName, Guid? excludeId = null)
    {
        var tenantId = _tenant.GetTenantId();
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Sellers.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.IsActive &&
            x.FirstName == firstName &&
            x.LastName == lastName &&
            (!excludeId.HasValue || x.Id != excludeId.Value));
    }
}