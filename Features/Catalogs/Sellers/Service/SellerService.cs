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

    public async Task<SellerEditModel?> GetAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);

        if (entity is null)
            return null;

        return new SellerEditModel
        {
            Id = entity.Id,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            IsActive = entity.IsActive
        };
    }

    public async Task SaveAsync(SellerEditModel model)
    {
        model.FirstName = model.FirstName.Trim();
        model.LastName = string.IsNullOrWhiteSpace(model.LastName) ? null : model.LastName.Trim();

        if (string.IsNullOrWhiteSpace(model.FirstName))
            throw new InvalidOperationException("El nombre del vendedor es obligatorio.");

        var exists = await _repo.ExistsByNameAsync(model.FirstName, model.LastName, model.Id);
        if (exists)
            throw new InvalidOperationException("Ya existe un vendedor con el mismo nombre.");

        if (model.Id is null)
        {
            var entity = new Seller
            {
                TenantId = _tenant.GetTenantId(),
                FirstName = model.FirstName,
                LastName = model.LastName,
                IsActive = true
            };

            await _repo.AddAsync(entity);
            await _audit.LogAsync("Seller", "Create", null, entity);
        }
        else
        {
            var entity = await _repo.GetAsync(model.Id.Value);

            if (entity is null)
                throw new InvalidOperationException("El vendedor no existe o no pertenece al tenant actual.");

            var old = new
            {
                entity.FirstName,
                entity.LastName,
                entity.IsActive
            };

            entity.FirstName = model.FirstName;
            entity.LastName = model.LastName;
            entity.IsActive = model.IsActive;

            await _repo.UpdateAsync(entity);
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
            entity.FirstName,
            entity.LastName,
            entity.IsActive
        };

        entity.IsActive = false;

        await _repo.UpdateAsync(entity);
        await _audit.LogAsync("Seller", "Delete", old, null);
    }
}