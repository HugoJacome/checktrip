using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

public class BoatRepository : BaseRepository<Boat>
{
    private readonly ITenantProvider _tenant;

    public BoatRepository(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantProvider tenant)
        : base(dbFactory)
    {
        _tenant = tenant;
    }

    public async Task<List<Boat>> GetAllAsync()
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Boats
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<Boat?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Boats
            .AsNoTracking()
            .Include(x => x.CrewMembers)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId);
    }

    public async Task CreateWithCrewAsync(Boat entity)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.TenantId = tenantId;

        var crew = entity.CrewMembers.ToList();
        entity.CrewMembers.Clear();

        db.Boats.Add(entity);

        foreach (var item in crew.Where(x => x.IsActive))
        {
            db.CrewMembers.Add(new CrewMember
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BoatId = entity.Id,
                FullName = item.FullName,
                DocumentNumber = item.DocumentNumber,
                Phone = item.Phone,
                CanBeCaptain = item.CanBeCaptain,
                CanBeSailor = item.CanBeSailor,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task UpdateWithCrewAsync(Boat entity)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var boat = await db.Boats
            .FirstOrDefaultAsync(x =>
                x.Id == entity.Id &&
                x.TenantId == tenantId);

        if (boat is null)
            throw new InvalidOperationException("El bote no existe o no pertenece al tenant actual.");

        boat.Name = entity.Name;
        boat.RegistrationNumber = entity.RegistrationNumber;
        boat.Capacity = entity.Capacity;
        boat.ExtraCapacity = entity.ExtraCapacity;
        boat.OwnerName = entity.OwnerName;
        boat.OwnerRuc = entity.OwnerRuc;
        boat.OwnerEmail = entity.OwnerEmail;
        boat.OwnerPhone = entity.OwnerPhone;
        boat.IsActive = entity.IsActive;

        var currentCrew = await db.CrewMembers
            .Where(x =>
                x.TenantId == tenantId &&
                x.BoatId == boat.Id)
            .ToListAsync();

        foreach (var item in entity.CrewMembers)
        {
            CrewMember? current = null;

            if (item.Id != Guid.Empty)
                current = currentCrew.FirstOrDefault(x => x.Id == item.Id);

            if (current is null)
            {
                db.CrewMembers.Add(new CrewMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BoatId = boat.Id,
                    FullName = item.FullName,
                    DocumentNumber = item.DocumentNumber,
                    Phone = item.Phone,
                    CanBeCaptain = item.CanBeCaptain,
                    CanBeSailor = item.CanBeSailor,
                    IsActive = item.IsActive
                });
            }
            else
            {
                current.FullName = item.FullName;
                current.DocumentNumber = item.DocumentNumber;
                current.Phone = item.Phone;
                current.CanBeCaptain = item.CanBeCaptain;
                current.CanBeSailor = item.CanBeSailor;
                current.IsActive = item.IsActive;
            }
        }

        await db.SaveChangesAsync();
    }
}