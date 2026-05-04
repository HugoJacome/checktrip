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
            IsActive = x.IsActive
        }).ToList();
    }

    public async Task<AgencyEditModel?> GetAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);
        if (entity is null)
            return null;

        return new AgencyEditModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Ruc = entity.Ruc,
            IsActive = entity.IsActive
        };
    }

    public async Task SaveAsync(AgencyEditModel model)
    {
        model.Name = model.Name.Trim();
        model.Ruc = string.IsNullOrWhiteSpace(model.Ruc) ? null : model.Ruc.Trim();

        if (string.IsNullOrWhiteSpace(model.Name))
            throw new InvalidOperationException("El nombre de la agencia es obligatorio.");

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
                IsActive = true
            };

            await _repo.AddAsync(entity);
            await _audit.LogAsync("Agency", "Create", null, entity);
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
                entity.IsActive
            };

            entity.Name = model.Name;
            entity.Ruc = model.Ruc;
            entity.IsActive = model.IsActive;

            await _repo.UpdateAsync(entity);
            await _audit.LogAsync("Agency", "Update", old, entity);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);

        if (entity is null)
            throw new InvalidOperationException("La agencia no existe o no pertenece al tenant actual.");

        var old = new
        {
            entity.Name,
            entity.Ruc,
            entity.IsActive
        };

        entity.IsActive = false;

        await _repo.UpdateAsync(entity);
        await _audit.LogAsync("Agency", "Delete", old, null);
    }
}