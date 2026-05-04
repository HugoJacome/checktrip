using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Infrastructure.Repositories;

public class ReservationRepository : BaseRepository<Reservation>
{
    private readonly ITenantProvider _tenant;

    public ReservationRepository(AppDbContext db, ITenantProvider tenant)
        : base(db)
    {
        _tenant = tenant;
    }

    public async Task<List<Reservation>> GetMonitorAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Reservations
            .Include(x => x.Agency)
            .Include(x => x.Seller)
            .Include(x => x.Items)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Reservation?> GetFullAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Reservations
            .Include(x => x.Items)
                .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<int> CountReservedAsync(Guid routeScheduleId, DateTime date, bool outbound)
    {
        var tenantId = _tenant.GetTenantId();
        var travelDate = DateOnly.FromDateTime(date);

        if (outbound)
        {
            return await _db.ReservationItems.CountAsync(x =>
                x.TenantId == tenantId &&
                x.OutboundRouteScheduleId == routeScheduleId &&
                x.OutboundTravelDate == travelDate &&
                x.Status != "Cancelled");
        }

        return await _db.ReservationItems.CountAsync(x =>
            x.TenantId == tenantId &&
            x.ReturnRouteScheduleId == routeScheduleId &&
            x.ReturnTravelDate == travelDate &&
            x.Status != "Cancelled");
    }

    public async Task<BoatRouteSchedule?> GetRouteScheduleAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.BoatRouteSchedules
            .Include(x => x.Boat)
            .Include(x => x.Route)
            .Include(x => x.Schedule)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId &&
                x.IsActive);
    }

    public async Task<Customer?> GetCustomerByDocumentAsync(string documentNumber)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Customers.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId &&
            x.DocumentNumber == documentNumber);
    }

    public async Task<int> CountReservationsTodayAsync()
    {
        var tenantId = _tenant.GetTenantId();
        var today = DateTime.UtcNow.Date;

        return await _db.Reservations.CountAsync(x =>
            x.TenantId == tenantId &&
            x.CreatedAt.Date == today);
    }
    public async Task<Reservation?> GetDetailAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Reservations
            .Include(x => x.Agency)
            .Include(x => x.Seller)
            .Include(x => x.Items)
                .ThenInclude(x => x.Customer)
            .Include(x => x.Items)
                .ThenInclude(x => x.OutboundRouteSchedule)
                    .ThenInclude(x => x!.Route)
            .Include(x => x.Items)
                .ThenInclude(x => x.OutboundRouteSchedule)
                    .ThenInclude(x => x!.Schedule)
            .Include(x => x.Items)
                .ThenInclude(x => x.ReturnRouteSchedule)
                    .ThenInclude(x => x!.Route)
            .Include(x => x.Items)
                .ThenInclude(x => x.ReturnRouteSchedule)
                    .ThenInclude(x => x!.Schedule)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }
}