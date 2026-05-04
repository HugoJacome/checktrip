using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

public class BoatRepository : BaseRepository<Boat>
{
    private readonly ITenantProvider _tenant;

    public BoatRepository(AppDbContext db, ITenantProvider tenant)
        : base(db)
    {
        _tenant = tenant;
    }

    public async Task<List<Boat>> GetAllAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Boats
            .Include(x => x.Agency)
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<Boat?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Boats
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }
}