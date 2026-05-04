using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Infrastructure.Audit;
using CheckTrip.Web.Infrastructure.Repositories;
using CheckTrip.Web.Infrastructure.Tenant;

namespace CheckTrip.Web.Features.Catalogs.Schedules.Services;

public class ScheduleService
{
    private readonly ScheduleRepository _repo;
    private readonly ITenantProvider _tenant;
    private readonly IAuditService _audit;

    public ScheduleService(
        ScheduleRepository repo,
        ITenantProvider tenant,
        IAuditService audit)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<List<ScheduleListItem>> GetAllAsync()
    {
        var data = await _repo.GetAllAsync();

        return data.Select(x => new ScheduleListItem
        {
            Id = x.Id,
            Name = x.Name,
            DepartureTime = x.DepartureTime
        }).ToList();
    }

    public async Task<ScheduleEditModel?> GetAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);
        if (entity is null)
            return null;

        return new ScheduleEditModel
        {
            Id = entity.Id,
            Name = entity.Name,
            DepartureTime = entity.DepartureTime
        };
    }

    public async Task SaveAsync(ScheduleEditModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            throw new Exception("El nombre del horario es obligatorio.");

        if (model.Id is null)
        {
            var entity = new Schedule
            {
                TenantId = _tenant.GetTenantId(),
                Name = model.Name.Trim(),
                DepartureTime = model.DepartureTime,
                IsActive = true
            };

            await _repo.AddAsync(entity);
            await _audit.LogAsync("Schedule", "Create", null, entity);
        }
        else
        {
            var entity = await _repo.GetAsync(model.Id.Value);
            if (entity is null)
                return;

            var old = new
            {
                entity.Name,
                entity.DepartureTime
            };

            entity.Name = model.Name.Trim();
            entity.DepartureTime = model.DepartureTime;

            await _repo.UpdateAsync(entity);
            await _audit.LogAsync("Schedule", "Update", old, entity);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);
        if (entity is null)
            return;

        entity.IsActive = false;

        await _repo.UpdateAsync(entity);
        await _audit.LogAsync("Schedule", "Delete", entity, null);
    }
}