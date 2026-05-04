using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Infrastructure.Repositories;

public class TicketRepository : BaseRepository<Ticket>
{
    private readonly ITenantProvider _tenant;

    public TicketRepository(AppDbContext db, ITenantProvider tenant)
        : base(db)
    {
        _tenant = tenant;
    }

    public async Task<List<Ticket>> GetByReservationAsync(Guid reservationId)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Tickets
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

        return await _db.Tickets
            .Include(x => x.ReservationItem)
                .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<bool> ExistsForReservationItemAsync(Guid reservationItemId)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Tickets.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.ReservationItemId == reservationItemId);
    }

    public async Task<int> CountTicketsTodayAsync()
    {
        var tenantId = _tenant.GetTenantId();
        var today = DateTime.UtcNow.Date;

        return await _db.Tickets.CountAsync(x =>
            x.TenantId == tenantId &&
            x.CreatedAt.Date == today);
    }
}