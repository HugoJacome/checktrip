using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

public class RouteRepository : BaseRepository<Route>
{
    private readonly ITenantProvider _tenant;

    public RouteRepository(AppDbContext db, ITenantProvider tenant)
        : base(db)
    {
        _tenant = tenant;
    }

    public async Task<List<Route>> GetAllByTenantAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Routes
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<Route?> GetByIdAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Routes
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }
}