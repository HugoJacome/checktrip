using CheckTrip.Web.Data.Entities;

public class RouteService
{
    private readonly RouteRepository _repo;
    private readonly ITenantProvider _tenant;
    private readonly IAuditService _audit;

    public RouteService(RouteRepository repo,
        ITenantProvider tenant,
        IAuditService audit)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<List<RouteListItem>> GetAllAsync()
    {
        var data = await _repo.GetAllByTenantAsync();

        return data.Select(x => new RouteListItem
        {
            Id = x.Id,
            Origin = x.Origin,
            Destination = x.Destination,
            Description = x.Description
        }).ToList();
    }

    public async Task<RouteEditModel?> GetAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;

        return new RouteEditModel
        {
            Id = entity.Id,
            Origin = entity.Origin,
            Destination = entity.Destination,
            Description = entity.Description
        };
    }

    public async Task SaveAsync(RouteEditModel model)
    {
        if (model.Id == null)
        {
            var entity = new Route
            {
                TenantId = _tenant.GetTenantId(),
                Origin = model.Origin,
                Destination = model.Destination,
                Description = model.Description
            };

            await _repo.AddAsync(entity);

            await _audit.LogAsync("Route", "Create", null, entity);
        }
        else
        {
            var entity = await _repo.GetByIdAsync(model.Id.Value);
            if (entity == null) return;

            var old = new
            {
                entity.Origin,
                entity.Destination,
                entity.Description
            };

            entity.Origin = model.Origin;
            entity.Destination = model.Destination;
            entity.Description = model.Description;

            await _repo.UpdateAsync(entity);

            await _audit.LogAsync("Route", "Update", old, entity);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return;

        entity.IsActive = false;

        await _repo.UpdateAsync(entity);

        await _audit.LogAsync("Route", "Delete", entity, null);
    }
}