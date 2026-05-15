using CheckAccess.Features.Catalogs.Agencies.Models;

public class SellerService
{
    private readonly SellerRepository _repo;
    private readonly ITenantProvider _tenant;
    private readonly IAuditService _audit;

    public SellerService(
        SellerRepository repo,
        ITenantProvider tenant,
        IAuditService audit)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<List<SellerListItem>> GetAllAsync()
    {
        var data = await _repo.GetAllAsync();

        return data.Select(x => new SellerListItem
        {
            Id = x.Id,
            FirstName = x.FirstName,
            LastName = x.LastName,
            IsActive = x.IsActive
        }).ToList();
    }

    public async Task<SellerEditModel> NewAsync()
    {
        var routes = await _repo.GetAvailableRoutesAsync();

        return new SellerEditModel
        {
            IsActive = true,
            SellsTransport = true,
            Routes = routes.Select(x => new RouteValueItem
            {
                RouteId = x.Id,
                RouteName = x.Description,
                IsSelected = false,
                Value = 0
            }).ToList()
        };
    }

    public async Task<SellerEditModel?> GetAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);

        if (entity is null)
            return null;

        var routes = await _repo.GetAvailableRoutesAsync();

        return new SellerEditModel
        {
            Id = entity.Id,
            DocumentType = entity.DocumentType,
            DocumentNumber = entity.DocumentNumber,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Address = entity.Address,
            Phone = entity.Phone,
            Email = entity.Email,
            StartDate = entity.StartDate,
            SellsTransport = entity.SellsTransport,
            SellsTours = entity.SellsTours,
            SellsServices = entity.SellsServices,
            HasCommission = entity.HasCommission,
            PaysReservation = entity.PaysReservation,
            IsActive = entity.IsActive,
            Routes = routes.Select(route =>
            {
                var current = entity.RouteCommissions.FirstOrDefault(x =>
                    x.RouteId == route.Id &&
                    x.IsActive);

                return new RouteValueItem
                {
                    RouteId = route.Id,
                    RouteName = route.Description,
                    IsSelected = current is not null,
                    Value = current?.Commission ?? 0
                };
            }).ToList()
        };
    }

    public async Task SaveAsync(SellerEditModel model)
    {
        Normalize(model);

        if (string.IsNullOrWhiteSpace(model.FirstName))
            throw new InvalidOperationException("El nombre del vendedor es obligatorio.");

        foreach (var route in model.Routes.Where(x => x.IsSelected))
        {
            if (route.Value < 0)
                throw new InvalidOperationException($"La comisión de la ruta {route.RouteName} no puede ser negativa.");
        }

        var exists = await _repo.ExistsByNameAsync(model.FirstName, model.LastName, model.Id);

        if (exists)
            throw new InvalidOperationException("Ya existe un vendedor con el mismo nombre.");

        if (model.Id is null)
        {
            var entity = new Seller
            {
                TenantId = _tenant.GetTenantId(),
                DocumentType = model.DocumentType,
                DocumentNumber = model.DocumentNumber,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Address = model.Address,
                Phone = model.Phone,
                Email = model.Email,
                StartDate = model.StartDate,
                SellsTransport = model.SellsTransport,
                SellsTours = model.SellsTours,
                SellsServices = model.SellsServices,
                HasCommission = model.HasCommission,
                PaysReservation = model.PaysReservation,
                IsActive = model.IsActive,
                RouteCommissions = model.Routes
                    .Where(x => x.IsSelected)
                    .Select(x => new SellerRouteCommission
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenant.GetTenantId(),
                        RouteId = x.RouteId,
                        Commission = x.Value,
                        IsActive = true
                    })
                    .ToList()
            };

            await _repo.CreateWithRoutesAsync(entity);

            var auditData = new
            {
                entity.Id,
                entity.TenantId,
                entity.DocumentType,
                entity.DocumentNumber,
                entity.FirstName,
                entity.LastName,
                entity.Address,
                entity.Phone,
                entity.Email,
                entity.StartDate,
                entity.SellsTransport,
                entity.SellsTours,
                entity.SellsServices,
                entity.HasCommission,
                entity.PaysReservation,
                entity.IsActive,
                Routes = entity.RouteCommissions.Select(x => new
                {
                    x.RouteId,
                    x.Commission,
                    x.IsActive
                }).ToList()
            };

            await _audit.LogAsync("Seller", "Create", null, auditData);
        }
        else
        {
            var entity = await _repo.GetAsync(model.Id.Value);

            if (entity is null)
                throw new InvalidOperationException("El vendedor no existe o no pertenece al tenant actual.");

            var old = new
            {
                entity.DocumentType,
                entity.DocumentNumber,
                entity.FirstName,
                entity.LastName,
                entity.Address,
                entity.Phone,
                entity.Email,
                entity.StartDate,
                entity.SellsTransport,
                entity.SellsTours,
                entity.SellsServices,
                entity.HasCommission,
                entity.PaysReservation,
                entity.IsActive,
                Routes = entity.RouteCommissions.Select(x => new
                {
                    x.RouteId,
                    x.Commission,
                    x.IsActive
                }).ToList()
            };

            entity.DocumentType = model.DocumentType;
            entity.DocumentNumber = model.DocumentNumber;
            entity.FirstName = model.FirstName;
            entity.LastName = model.LastName;
            entity.Address = model.Address;
            entity.Phone = model.Phone;
            entity.Email = model.Email;
            entity.StartDate = model.StartDate;
            entity.SellsTransport = model.SellsTransport;
            entity.SellsTours = model.SellsTours;
            entity.SellsServices = model.SellsServices;
            entity.HasCommission = model.HasCommission;
            entity.PaysReservation = model.PaysReservation;
            entity.IsActive = model.IsActive;

            SyncRoutes(entity, model);

            await _repo.UpdateWithRoutesAsync(entity);
            await _audit.LogAsync("Seller", "Update", old, entity);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);

        if (entity is null)
            throw new InvalidOperationException("El vendedor no existe o no pertenece al tenant actual.");

        var old = new
        {
            entity.DocumentType,
            entity.DocumentNumber,
            entity.FirstName,
            entity.LastName,
            entity.Address,
            entity.Phone,
            entity.Email,
            entity.StartDate,
            entity.SellsTransport,
            entity.SellsTours,
            entity.SellsServices,
            entity.HasCommission,
            entity.PaysReservation,
            entity.IsActive,
            Routes = entity.RouteCommissions.Select(x => new
            {
                x.RouteId,
                x.Commission,
                x.IsActive
            }).ToList()
        };

        entity.IsActive = false;

        foreach (var commission in entity.RouteCommissions)
            commission.IsActive = false;

        await _repo.UpdateWithRoutesAsync(entity);
        await _audit.LogAsync("Seller", "Delete", old, null);
    }

    private void SyncRoutes(Seller entity, SellerEditModel model)
    {
        var tenantId = _tenant.GetTenantId();

        entity.RouteCommissions = model.Routes
            .Select(item => new SellerRouteCommission
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SellerId = entity.Id,
                RouteId = item.RouteId,
                Commission = item.Value,
                IsActive = item.IsSelected
            })
            .ToList();
    }

    private static void Normalize(SellerEditModel model)
    {
        model.DocumentType = Clean(model.DocumentType);
        model.DocumentNumber = Clean(model.DocumentNumber);
        model.FirstName = model.FirstName.Trim();
        model.LastName = Clean(model.LastName);
        model.Address = Clean(model.Address);
        model.Phone = Clean(model.Phone);
        model.Email = Clean(model.Email);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}