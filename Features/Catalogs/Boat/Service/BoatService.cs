using CheckAccess.Features.Catalogs.Boat.Models;
using CheckTrip.Web.Data.Entities;

public class BoatService
{
    private readonly BoatRepository _repo;
    private readonly ITenantProvider _tenant;
    private readonly IAuditService _audit;

    public BoatService(
        BoatRepository repo,
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
            ExtraCapacity = x.ExtraCapacity
        }).ToList();
    }

    public async Task<BoatEditModel?> GetAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);

        if (entity is null)
            return null;

        return new BoatEditModel
        {
            Id = entity.Id,
            Name = entity.Name,
            RegistrationNumber = entity.RegistrationNumber,
            Capacity = entity.Capacity,
            ExtraCapacity = entity.ExtraCapacity,
            OwnerName = entity.OwnerName,
            OwnerRuc = entity.OwnerRuc,
            OwnerEmail = entity.OwnerEmail,
            OwnerPhone = entity.OwnerPhone,
            IsActive = entity.IsActive,
            CrewMembers = entity.CrewMembers
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CanBeCaptain)
                .ThenBy(x => x.FullName)
                .Select(x => new CrewMemberEditModel
                {
                    Id = x.Id,
                    FullName = x.FullName,
                    DocumentNumber = x.DocumentNumber,
                    Phone = x.Phone,
                    CanBeCaptain = x.CanBeCaptain,
                    CanBeSailor = x.CanBeSailor,
                    IsActive = x.IsActive
                }).ToList()
        };
    }

    public async Task SaveAsync(BoatEditModel model)
    {
        Normalize(model);

        ValidateCrewMembers(model);

        if (string.IsNullOrWhiteSpace(model.Name))
            throw new InvalidOperationException("El nombre del bote es obligatorio.");

        if (model.Capacity <= 0)
            throw new InvalidOperationException("La capacidad debe ser mayor a 0.");

        if (model.ExtraCapacity < 0)
            throw new InvalidOperationException("La capacidad extra no puede ser negativa.");

        foreach (var crew in model.CrewMembers.Where(x => x.IsActive))
        {
            if (string.IsNullOrWhiteSpace(crew.FullName))
                throw new InvalidOperationException("Existen tripulantes sin nombre.");

            if (!crew.CanBeCaptain && !crew.CanBeSailor)
                throw new InvalidOperationException($"El tripulante {crew.FullName} debe tener al menos un rol.");
        }

        if (model.Id is null)
        {
            var entity = BuildEntity(model);

            await _repo.CreateWithCrewAsync(entity);

            await _audit.LogAsync("Boat", "Create", null, ToAudit(entity));
        }
        else
        {
            var oldEntity = await _repo.GetAsync(model.Id.Value);

            if (oldEntity is null)
                throw new InvalidOperationException("El bote no existe o no pertenece al tenant actual.");

            var old = ToAudit(oldEntity);

            var entity = BuildEntity(model);
            entity.Id = model.Id.Value;

            await _repo.UpdateWithCrewAsync(entity);

            await _audit.LogAsync("Boat", "Update", old, ToAudit(entity));
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetAsync(id);

        if (entity is null)
            throw new InvalidOperationException("El bote no existe o no pertenece al tenant actual.");

        var old = ToAudit(entity);

        entity.IsActive = false;

        foreach (var crew in entity.CrewMembers)
            crew.IsActive = false;

        await _repo.UpdateWithCrewAsync(entity);

        await _audit.LogAsync("Boat", "Delete", old, null);
    }

    private Boat BuildEntity(BoatEditModel model)
    {
        var tenantId = _tenant.GetTenantId();

        return new Boat
        {
            Id = model.Id ?? Guid.NewGuid(),
            TenantId = tenantId,
            Name = model.Name,
            RegistrationNumber = model.RegistrationNumber,
            Capacity = model.Capacity,
            ExtraCapacity = model.ExtraCapacity,
            OwnerName = model.OwnerName,
            OwnerRuc = model.OwnerRuc,
            OwnerEmail = model.OwnerEmail,
            OwnerPhone = model.OwnerPhone,
            IsActive = model.IsActive,
            CrewMembers = model.CrewMembers
                .Select(x => new CrewMember
                {
                    Id = x.Id ?? Guid.Empty,
                    TenantId = tenantId,
                    BoatId = model.Id ?? Guid.Empty,
                    FullName = x.FullName,
                    DocumentNumber = x.DocumentNumber,
                    Phone = x.Phone,
                    CanBeCaptain = x.CanBeCaptain,
                    CanBeSailor = x.CanBeSailor,
                    IsActive = x.IsActive
                })
                .ToList()
        };
    }

    private static void Normalize(BoatEditModel model)
    {
        model.Name = model.Name.Trim();
        model.RegistrationNumber = Clean(model.RegistrationNumber);
        model.OwnerName = Clean(model.OwnerName);
        model.OwnerRuc = Clean(model.OwnerRuc);
        model.OwnerEmail = Clean(model.OwnerEmail);
        model.OwnerPhone = Clean(model.OwnerPhone);

        foreach (var crew in model.CrewMembers)
        {
            crew.FullName = crew.FullName.Trim();
            crew.DocumentNumber = Clean(crew.DocumentNumber);
            crew.Phone = Clean(crew.Phone);
        }
    }

    private static object ToAudit(Boat entity)
    {
        return new
        {
            entity.Id,
            entity.TenantId,
            entity.Name,
            entity.RegistrationNumber,
            entity.Capacity,
            entity.ExtraCapacity,
            entity.OwnerName,
            entity.OwnerRuc,
            entity.OwnerEmail,
            entity.OwnerPhone,
            entity.IsActive,
            CrewMembers = entity.CrewMembers.Select(x => new
            {
                x.Id,
                x.FullName,
                x.DocumentNumber,
                x.Phone,
                x.CanBeCaptain,
                x.CanBeSailor,
                x.IsActive
            }).ToList()
        };
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    private static void ValidateCrewMembers(BoatEditModel model)
    {
        var activeCrew = model.CrewMembers
            .Where(x => x.IsActive)
            .ToList();

        var captainCount = activeCrew.Count(x => x.CanBeCaptain);
        var sailorCount = activeCrew.Count(x => x.CanBeSailor);

        if (captainCount != 1)
            throw new InvalidOperationException("Debe existir exactamente un capitán activo.");

        if (sailorCount > 2)
            throw new InvalidOperationException("Solo se permiten máximo 2 marineros activos.");

        foreach (var crew in activeCrew)
        {
            if (string.IsNullOrWhiteSpace(crew.FullName))
                throw new InvalidOperationException("Todo tripulante activo debe tener nombre.");

            if (crew.CanBeCaptain && crew.CanBeSailor)
                throw new InvalidOperationException("Un tripulante no puede ser capitán y marinero al mismo tiempo.");
        }
    }
}