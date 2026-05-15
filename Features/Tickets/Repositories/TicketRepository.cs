using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Infrastructure.Repositories;

public class TicketRepository : BaseRepository<Ticket>
{
    private readonly ITenantProvider _tenant;

    public TicketRepository(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantProvider tenant)
        : base(dbFactory)
    {
        _tenant = tenant;
    }

    public async Task<List<Ticket>> GetByReservationAsync(Guid reservationId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets
            .AsNoTracking()
            .Include(x => x.ReservationItem)
                .ThenInclude(x => x.Customer)
            .Where(x =>
                x.TenantId == tenantId &&
                x.ReservationItem.ReservationId == reservationId)
            .OrderBy(x => x.TicketNumber)
            .ToListAsync();
    }

    public async Task<Ticket?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets
            .Include(x => x.ReservationItem)
                .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<bool> ExistsForReservationItemAsync(Guid reservationItemId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.ReservationItemId == reservationItemId);
    }

    public async Task<int> CountTicketsTodayAsync()
    {
        var tenantId = _tenant.GetTenantId();
        var today = DateTime.UtcNow.Date;

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets.CountAsync(x =>
            x.TenantId == tenantId &&
            x.CreatedAt.Date == today);
    }
    public async Task<List<Ticket>> GetByBoatAndDateAsync(Guid boatId, DateTime tripDate)
    {
        var tenantId = _tenant.GetTenantId();
        var date = tripDate.Date;

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets
            .AsNoTracking()
            .Include(x => x.ReservationItem!)
                .ThenInclude(x => x.Customer)
            .Where(x =>
                x.TenantId == tenantId &&
                x.BoatId == boatId &&
                x.TripDate.HasValue &&
                x.TripDate.Value.Date == date)
            .OrderBy(x => x.TicketNumber)
            .ToListAsync();
    }

    public async Task<int> CountTicketsByDateAsync(DateTime date)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets.CountAsync(x =>
            x.TenantId == tenantId &&
            x.CreatedAt.Date == date.Date);
    }
}