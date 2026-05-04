public class BoatRouteScheduleService
{
    private readonly BoatRouteScheduleRepository _repo;
    private readonly ITenantProvider _tenant;
    private readonly IAuditService _audit;

    public BoatRouteScheduleService(
        BoatRouteScheduleRepository repo,
        ITenantProvider tenant,
        IAuditService audit)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<List<BoatRouteScheduleListItem>> GetAllAsync()
    {
        var data = await _repo.GetAllAsync();

        return data.Select(x => new BoatRouteScheduleListItem
        {
            Id = x.Id,
            Boat = x.Boat.Name,
            Route = $"{x.Route.Origin} - {x.Route.Destination}",
            Schedule = x.Schedule.Name,
            Price = x.Price,
            Color = x.Color,
            Days = x.Days
        }).ToList();
    }

    public async Task<BoatRouteScheduleEditModel?> GetAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);
        if (entity == null) return null;

        return new BoatRouteScheduleEditModel
        {
            Id = entity.Id,
            BoatId = entity.BoatId,
            RouteId = entity.RouteId,
            ScheduleId = entity.ScheduleId,
            Price = entity.Price,
            Color = entity.Color,
            Days = entity.Days
        };
    }

    public async Task SaveAsync(BoatRouteScheduleEditModel model)
    {
        if (model.Price < 0)
            throw new Exception("Precio inválido");

        if (model.Id == null)
        {
            var exists = await _repo.ExistsAsync(model.BoatId, model.RouteId, model.ScheduleId);
            if (exists)
                throw new Exception("Ya existe esa combinación");

            var entity = new BoatRouteSchedule
            {
                TenantId = _tenant.GetTenantId(),
                BoatId = model.BoatId,
                RouteId = model.RouteId,
                ScheduleId = model.ScheduleId,
                Price = model.Price,
                Color = model.Color,
                Days = model.Days,
                IsActive = true
            };

            await _repo.AddAsync(entity);
            await _audit.LogAsync("BoatRouteSchedule", "Create", null, entity);
        }
        else
        {
            var entity = await _repo.GetAsync(model.Id.Value);
            if (entity == null) return;

            var old = new
            {
                entity.Price,
                entity.Color,
                entity.Days
            };

            entity.Price = model.Price;
            entity.Color = model.Color;
            entity.Days = model.Days;

            await _repo.UpdateAsync(entity);
            await _audit.LogAsync("BoatRouteSchedule", "Update", old, entity);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);
        if (entity == null) return;

        entity.IsActive = false;

        await _repo.UpdateAsync(entity);
        await _audit.LogAsync("BoatRouteSchedule", "Delete", entity, null);
    }
}