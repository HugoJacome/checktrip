using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Infrastructure.Repositories;

public class RouteRepository : BaseRepository<Route>
{
    private readonly ITenantProvider _tenant;

    public RouteRepository(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantProvider tenant)
        : base(dbFactory)
    {
        _tenant = tenant;
    }

    public async Task<List<Route>> GetAllByTenantAsync()
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Routes
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Origin)
            .ThenBy(x => x.Destination)
            .ToListAsync();
    }

    public async Task<Route?> GetByIdAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Routes
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }
}