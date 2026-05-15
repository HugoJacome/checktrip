using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
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
            .AsNoTracking()
            .Include(x => x.RouteCommissions)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId);
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

    public async Task UpdateWithRoutesAsync(Seller entity)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var seller = await db.Sellers
            .FirstOrDefaultAsync(x =>
                x.Id == entity.Id &&
                x.TenantId == tenantId);

        if (seller is null)
            throw new InvalidOperationException("El vendedor no existe o no pertenece al tenant actual.");

        seller.DocumentType = entity.DocumentType;
        seller.DocumentNumber = entity.DocumentNumber;
        seller.FirstName = entity.FirstName;
        seller.LastName = entity.LastName;
        seller.Address = entity.Address;
        seller.Phone = entity.Phone;
        seller.Email = entity.Email;
        seller.StartDate = entity.StartDate;
        seller.SellsTransport = entity.SellsTransport;
        seller.SellsTours = entity.SellsTours;
        seller.SellsServices = entity.SellsServices;
        seller.HasCommission = entity.HasCommission;
        seller.PaysReservation = entity.PaysReservation;
        seller.IsActive = entity.IsActive;

        var currentCommissions = await db.SellerRouteCommissions
            .Where(x =>
                x.TenantId == tenantId &&
                x.SellerId == entity.Id)
            .ToListAsync();

        foreach (var item in entity.RouteCommissions)
        {
            var current = currentCommissions
                .FirstOrDefault(x => x.RouteId == item.RouteId);

            if (current is null)
            {
                db.SellerRouteCommissions.Add(new SellerRouteCommission
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SellerId = entity.Id,
                    RouteId = item.RouteId,
                    Commission = item.Commission,
                    IsActive = item.IsActive
                });
            }
            else
            {
                current.Commission = item.Commission;
                current.IsActive = item.IsActive;
            }
        }

        await db.SaveChangesAsync();
    }
    public async Task CreateWithRoutesAsync(Seller entity)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        entity.Id = entity.Id == Guid.Empty
            ? Guid.NewGuid()
            : entity.Id;

        entity.TenantId = tenantId;

        var routes = entity.RouteCommissions.ToList();

        entity.RouteCommissions.Clear();

        db.Sellers.Add(entity);

        foreach (var item in routes.Where(x => x.IsActive))
        {
            db.SellerRouteCommissions.Add(new SellerRouteCommission
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SellerId = entity.Id,
                RouteId = item.RouteId,
                Commission = item.Commission,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
    }
}