using CheckTrip.Web.Data;
using CheckTrip.Web.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

public class SellerRepository : BaseRepository<Seller>
{
    private readonly ITenantProvider _tenant;

    public SellerRepository(AppDbContext db, ITenantProvider tenant)
        : base(db)
    {
        _tenant = tenant;
    }

    public async Task<List<Seller>> GetAllAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Sellers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ToListAsync();
    }

    public async Task<Seller?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Sellers
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<bool> ExistsByNameAsync(string firstName, string? lastName, Guid? excludeId = null)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Sellers.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.IsActive &&
            x.FirstName == firstName &&
            x.LastName == lastName &&
            (!excludeId.HasValue || x.Id != excludeId.Value));
    }
}