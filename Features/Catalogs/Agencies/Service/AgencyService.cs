using CheckAccess.Features.Catalogs.Agencies.Models;
using CheckTrip.Web.Data.Entities;

public class AgencyService
{
    private readonly AgencyRepository _repo;
    private readonly ITenantProvider _tenant;
    private readonly IAuditService _audit;

    public AgencyService(
        AgencyRepository repo,
        ITenantProvider tenant,
        IAuditService audit)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<List<AgencyListItem>> GetAllAsync()
    {
        var data = await _repo.GetAllAsync();

        return data.Select(x => new AgencyListItem
        {
            Id = x.Id,
            Name = x.Name,
            Ruc = x.Ruc,
            Phone = x.Phone,
            Email = x.Email,
            ContactPerson = x.ContactPerson,
            ContactPhone = x.ContactPhone,
            Type = x.Type,
            IsActive = x.IsActive
        }).ToList();
    }

    public async Task<AgencyEditModel?> GetAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);

        if (entity is null)
            return null;

        var routes = await _repo.GetAvailableRoutesAsync();

        return new AgencyEditModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Ruc = entity.Ruc,
            Address = entity.Address,
            Phone = entity.Phone,
            Email = entity.Email,
            ContactPerson = entity.ContactPerson,
            ContactPhone = entity.ContactPhone,
            Type = entity.Type,
            IsActive = entity.IsActive,
            Routes = routes.Select(route =>
            {
                var current = entity.RouteRates.FirstOrDefault(x =>
                    x.RouteId == route.Id &&
                    x.IsActive);

                return new RouteValueItem
                {
                    RouteId = route.Id,
                    RouteName = route.Description,
                    IsSelected = current is not null,
                    Value = current?.Price ?? 0
                };
            }).ToList()
        };
    }

    public async Task<AgencyEditModel> NewAsync()
    {
        var routes = await _repo.GetAvailableRoutesAsync();

        return new AgencyEditModel
        {
            IsActive = true,
            Routes = routes.Select(x => new RouteValueItem
            {
                RouteId = x.Id,
                RouteName = x.Description,
                IsSelected = false,
                Value = 0
            }).ToList()
        };
    }

    public async Task SaveAsync(AgencyEditModel model)
    {
        Normalize(model);

        if (string.IsNullOrWhiteSpace(model.Name))
            throw new InvalidOperationException("El nombre de la agencia es obligatorio.");

        foreach (var route in model.Routes.Where(x => x.IsSelected))
        {
            if (route.Value < 0)
                throw new InvalidOperationException($"El valor de la ruta {route.RouteName} no puede ser negativo.");
        }

        var exists = await _repo.ExistsByNameAsync(model.Name, model.Id);

        if (exists)
            throw new InvalidOperationException("Ya existe una agencia con el mismo nombre.");

        if (model.Id is null)
        {
            var entity = new Agency
            {
                TenantId = _tenant.GetTenantId(),
                Name = model.Name,
                Ruc = model.Ruc,
                Address = model.Address,
                Phone = model.Phone,
                Email = model.Email,
                ContactPerson = model.ContactPerson,
                ContactPhone = model.ContactPhone,
                Type = model.Type,
                IsActive = model.IsActive,
                RouteRates = model.Routes
                    .Where(x => x.IsSelected)
                    .Select(x => new AgencyRouteRate
                    {
                        TenantId = _tenant.GetTenantId(),
                        RouteId = x.RouteId,
                        Price = x.Value,
                        IsActive = true
                    })
                    .ToList()
            };

            await _repo.CreateWithRoutesAsync(entity); 
            
            var auditData = new
            {
                entity.Id,
                entity.TenantId,
                entity.Name,
                entity.Ruc,
                entity.Address,
                entity.Phone,
                entity.Email,
                entity.ContactPerson,
                entity.ContactPhone,
                entity.Type,
                entity.IsActive,
                Routes = entity.RouteRates.Select(x => new
                {
                    x.RouteId,
                    x.Price,
                    x.IsActive
                }).ToList()
            };

            await _audit.LogAsync("Agency", "Create", null, auditData);
        }
        else
        {
            var entity = await _repo.GetAsync(model.Id.Value);

            if (entity is null)
                throw new InvalidOperationException("La agencia no existe o no pertenece al tenant actual.");

            var old = new
            {
                entity.Name,
                entity.Ruc,
                entity.Address,
                entity.Phone,
                entity.Email,
                entity.ContactPerson,
                entity.ContactPhone,
                entity.Type,
                entity.IsActive,
                Routes = entity.RouteRates.Select(x => new
                {
                    x.RouteId,
                    x.Price,
                    x.IsActive
                }).ToList()
            };

            entity.Name = model.Name;
            entity.Ruc = model.Ruc;
            entity.Address = model.Address;
            entity.Phone = model.Phone;
            entity.Email = model.Email;
            entity.ContactPerson = model.ContactPerson;
            entity.ContactPhone = model.ContactPhone;
            entity.Type = model.Type;
            entity.IsActive = model.IsActive;

            SyncRoutes(entity, model);

            await _repo.SaveWithRoutesAsync(entity);
            await _audit.LogAsync("Agency", "Update", old, entity);
        }
    }

    private void SyncRoutes(Agency entity, AgencyEditModel model)
    {
        var tenantId = _tenant.GetTenantId();

        entity.RouteRates.Clear();

        foreach (var item in model.Routes)
        {
            entity.RouteRates.Add(new AgencyRouteRate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AgencyId = entity.Id,
                RouteId = item.RouteId,
                Price = item.Value,
                IsActive = item.IsSelected
            });
        }
    }
    private static void Normalize(AgencyEditModel model)
    {
        model.Name = model.Name.Trim();
        model.Ruc = Clean(model.Ruc);
        model.Address = Clean(model.Address);
        model.Phone = Clean(model.Phone);
        model.Email = Clean(model.Email);
        model.ContactPerson = Clean(model.ContactPerson);
        model.ContactPhone = Clean(model.ContactPhone);
        model.Type = Clean(model.Type);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}