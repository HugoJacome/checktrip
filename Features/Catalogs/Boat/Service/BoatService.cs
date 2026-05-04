public class BoatService
{
    private readonly BoatRepository _repo;
    private readonly ITenantProvider _tenant;
    private readonly IAuditService _audit;

    public BoatService(BoatRepository repo,
        ITenantProvider tenant,
        IAuditService audit)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<List<BoatListItem>> GetAllAsync()
    {
        var data = await _repo.GetAllAsync();

        return data.Select(x => new BoatListItem
        {
            Id = x.Id,
            Name = x.Name,
            Capacity = x.Capacity,
            ExtraCapacity = x.ExtraCapacity,
            AgencyName = x.Agency?.Name
        }).ToList();
    }

    public async Task<BoatEditModel?> GetAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);
        if (entity == null) return null;

        return new BoatEditModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Capacity = entity.Capacity,
            ExtraCapacity = entity.ExtraCapacity,
            AgencyId = entity.AgencyId
        };
    }

    public async Task SaveAsync(BoatEditModel model)
    {
        if (model.Capacity <= 0)
            throw new Exception("La capacidad debe ser mayor a 0");

        if (model.ExtraCapacity < 0)
            throw new Exception("Capacidad extra inválida");

        if (model.Id == null)
        {
            var entity = new Boat
            {
                TenantId = _tenant.GetTenantId(),
                Name = model.Name,
                Capacity = model.Capacity,
                ExtraCapacity = model.ExtraCapacity,
                AgencyId = model.AgencyId
            };

            await _repo.AddAsync(entity);
            await _audit.LogAsync("Boat", "Create", null, entity);
        }
        else
        {
            var entity = await _repo.GetAsync(model.Id.Value);
            if (entity == null) return;

            var old = new
            {
                entity.Name,
                entity.Capacity,
                entity.ExtraCapacity,
                entity.AgencyId
            };

            entity.Name = model.Name;
            entity.Capacity = model.Capacity;
            entity.ExtraCapacity = model.ExtraCapacity;
            entity.AgencyId = model.AgencyId;

            await _repo.UpdateAsync(entity);
            await _audit.LogAsync("Boat", "Update", old, entity);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);
        if (entity == null) return;

        entity.IsActive = false;

        await _repo.UpdateAsync(entity);
        await _audit.LogAsync("Boat", "Delete", entity, null);
    }
}