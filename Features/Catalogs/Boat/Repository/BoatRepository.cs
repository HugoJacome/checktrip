using CheckTrip.Web.Data;
using CheckTrip.Web.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

public class BoatRepository : BaseRepository<Boat>
{
    private readonly ITenantProvider _tenant;

    public BoatRepository(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantProvider tenant)
        : base(dbFactory)
    {
        _tenant = tenant;
    }

    public async Task<List<Boat>> GetAllAsync()
    {
        var tenantId = _tenant.GetTenantId();
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Boats
            .Include(x => x.Agency)
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<Boat?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Boats
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }
}