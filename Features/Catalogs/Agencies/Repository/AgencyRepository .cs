using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
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

        var agency = await db.Agencies
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId);

        if (agency is null)
            return null;

        agency.RouteRates = await db.AgencyRouteRates
            .Where(x =>
                x.TenantId == tenantId &&
                x.AgencyId == agency.Id)
            .ToListAsync();

        return agency;
    }

    public async Task<List<TripRoute>> GetAvailableRoutesAsync()
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Routes
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Description)
            .ToListAsync();
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
    public async Task SaveWithRoutesAsync(Agency entity)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var existing = await db.Agencies
            .FirstOrDefaultAsync(x =>
                x.Id == entity.Id &&
                x.TenantId == tenantId);

        if (existing is null)
            throw new InvalidOperationException("La agencia no existe o no pertenece al tenant actual.");

        existing.Name = entity.Name;
        existing.Ruc = entity.Ruc;
        existing.Address = entity.Address;
        existing.Phone = entity.Phone;
        existing.Email = entity.Email;
        existing.ContactPerson = entity.ContactPerson;
        existing.ContactPhone = entity.ContactPhone;
        existing.Type = entity.Type;
        existing.IsActive = entity.IsActive;

        var currentRates = await db.AgencyRouteRates
            .Where(x =>
                x.TenantId == tenantId &&
                x.AgencyId == existing.Id)
            .ToListAsync();

        foreach (var incoming in entity.RouteRates)
        {
            var current = currentRates.FirstOrDefault(x => x.RouteId == incoming.RouteId);

            if (current is null)
            {
                db.AgencyRouteRates.Add(new AgencyRouteRate
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    AgencyId = existing.Id,
                    RouteId = incoming.RouteId,
                    Price = incoming.Price,
                    IsActive = incoming.IsActive
                });
            }
            else
            {
                current.Price = incoming.Price;
                current.IsActive = incoming.IsActive;
            }
        }

        await db.SaveChangesAsync();
    }
    public async Task CreateWithRoutesAsync(Agency entity)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.TenantId = tenantId;

        var routeRates = entity.RouteRates.ToList();
        entity.RouteRates.Clear();

        db.Agencies.Add(entity);

        foreach (var rate in routeRates.Where(x => x.IsActive))
        {
            db.AgencyRouteRates.Add(new AgencyRouteRate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AgencyId = entity.Id,
                RouteId = rate.RouteId,
                Price = rate.Price,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
    }
}