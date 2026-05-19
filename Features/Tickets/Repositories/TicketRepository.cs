using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Features.Tickets.Models;
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

    public Task<Guid> GetTenantIdAsync()
    {
        return Task.FromResult(_tenant.GetTenantId());
    }

    public async Task<List<ReservationPassengerTrip>> GetReservationPassengerTripsAsync(Guid reservationId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.ReservationPassengerTrips
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.BoatRouteSchedule)
            .Where(x =>
                x.TenantId == tenantId &&
                x.ReservationId == reservationId &&
                x.Status != "Cancelled")
            .OrderBy(x => x.TravelDate)
            .ThenBy(x => x.SegmentType)
            .ToListAsync();
    }

    public async Task<Reservation?> GetReservationWithPassengerTripsAsync(Guid reservationId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Reservations
            .AsNoTracking()
            .Include(x => x.PassengerTrips)
                .ThenInclude(x => x.BoatRouteSchedule)
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Id == reservationId);
    }

    public async Task<List<Ticket>> GetTicketsByPassengerTripIdsAsync(List<Guid> passengerTripIds)
    {
        var tenantId = _tenant.GetTenantId();

        if (!passengerTripIds.Any())
            return [];

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.ReservationPassengerTripId.HasValue &&
                passengerTripIds.Contains(x.ReservationPassengerTripId.Value))
            .OrderBy(x => x.TicketNumber)
            .ToListAsync();
    }

    public async Task<List<Ticket>> GetByReservationAsync(Guid reservationId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets
            .AsNoTracking()
            .Include(x => x.ReservationPassengerTrip)
                .ThenInclude(x => x!.Customer)
            .Where(x =>
                x.TenantId == tenantId &&
                x.ReservationPassengerTrip != null &&
                x.ReservationPassengerTrip.ReservationId == reservationId)
            .OrderBy(x => x.TicketNumber)
            .ToListAsync();
    }

    public async Task<Ticket?> GetAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets
            .AsNoTracking()
            .Include(x => x.ReservationPassengerTrip)
                .ThenInclude(x => x!.Customer)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
    }

    public async Task<bool> ExistsForReservationPassengerTripAsync(Guid reservationPassengerTripId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.ReservationPassengerTripId == reservationPassengerTripId);
    }

    public async Task<int> CountTicketsTodayAsync()
    {
        var tenantId = _tenant.GetTenantId();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets.CountAsync(x =>
            x.TenantId == tenantId &&
            x.CreatedAt >= today &&
            x.CreatedAt < tomorrow);
    }

    public async Task<List<Ticket>> GetByBoatAndDateAsync(Guid boatId, DateTime tripDate)
    {
        var date = DateOnly.FromDateTime(tripDate.Date);
        return await GetTicketsByBoatAndDateAsync(boatId, date);
    }

    public async Task<List<Ticket>> GetTicketsByBoatAndDateAsync(Guid boatId, DateOnly tripDate)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets
            .AsNoTracking()
            .Include(x => x.ReservationPassengerTrip)
                .ThenInclude(x => x!.Customer)
            .Where(x =>
                x.TenantId == tenantId &&
                x.BoatId == boatId &&
                x.TripDate.HasValue &&
                x.TripDate.Value == tripDate)
            .OrderBy(x => x.TicketNumber)
            .ToListAsync();
    }

    public async Task<int> CountTicketsByDateAsync(DateTime date)
    {
        var tenantId = _tenant.GetTenantId();
        var start = date.Date;
        var end = start.AddDays(1);

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tickets.CountAsync(x =>
            x.TenantId == tenantId &&
            x.CreatedAt >= start &&
            x.CreatedAt < end);
    }

    public async Task<List<ReservationPassengerTrip>> GetMonitorPassengerTripsByBoatAndDateAsync(
        Guid boatId,
        DateOnly tripDate)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.ReservationPassengerTrips
            .AsNoTracking()
            .Include(x => x.Reservation)
            .Include(x => x.Customer)
            .Include(x => x.BoatRouteSchedule)
                .ThenInclude(x => x.Route)
            .Include(x => x.BoatRouteSchedule)
                .ThenInclude(x => x.Schedule)
            .Where(x =>
                x.TenantId == tenantId &&
                x.Status != "Cancelled" &&
                x.Reservation.Status != "Cancelled" &&
                x.TravelDate == tripDate &&
                x.BoatRouteSchedule.BoatId == boatId)
            .ToListAsync();
    }

    public async Task<List<ReservationPassengerTrip>> GetMonitorPassengerTripsByScheduleAndDateAsync(
        Guid boatRouteScheduleId,
        DateOnly tripDate)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.ReservationPassengerTrips
            .AsNoTracking()
            .Include(x => x.Reservation)
            .Include(x => x.Customer)
            .Include(x => x.BoatRouteSchedule)
                .ThenInclude(x => x.Route)
            .Include(x => x.BoatRouteSchedule)
                .ThenInclude(x => x.Schedule)
            .Where(x =>
                x.TenantId == tenantId &&
                x.Status != "Cancelled" &&
                x.Reservation.Status != "Cancelled" &&
                x.TravelDate == tripDate &&
                x.BoatRouteScheduleId == boatRouteScheduleId)
            .ToListAsync();
    }

    public async Task<BoatRouteSchedule?> GetBoatRouteScheduleAsync(Guid boatRouteScheduleId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Id == boatRouteScheduleId);
    }

    public async Task<List<Guid>> GetActiveScheduleIdsByBoatAsync(Guid boatId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatRouteSchedules
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.BoatId == boatId &&
                x.IsActive)
            .Select(x => x.Id)
            .ToListAsync();
    }

    public async Task<bool> IsTripLockedAsync(Guid boatRouteScheduleId, DateOnly tripDate)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatDailyTrips.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.BoatRouteScheduleId == boatRouteScheduleId &&
            x.TripDate == tripDate &&
            (x.Status == BoatDailyTripStatus.DocumentGenerated ||
             x.Status == BoatDailyTripStatus.Closed));
    }

    public async Task AddAsync(Ticket ticket)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
    }

    public async Task AddRangeAsync(List<Ticket> tickets)
    {
        if (!tickets.Any())
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();

        db.Tickets.AddRange(tickets);
        await db.SaveChangesAsync();
    }

    public async Task<TicketPrintResult?> MarkPrintedAsync(Guid ticketId, Guid userId)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var ticket = await db.Tickets.FirstOrDefaultAsync(x =>
            x.Id == ticketId &&
            x.TenantId == tenantId);

        if (ticket is null)
            return null;

        var oldValue = new
        {
            ticket.Id,
            ticket.TicketNumber,
            ticket.IsPrinted,
            ticket.PrintedAt,
            ticket.ReprintCount
        };

        var action = ticket.IsPrinted ? "Reprint" : "Print";

        if (!ticket.IsPrinted)
        {
            ticket.IsPrinted = true;
            ticket.PrintedAt = DateTime.UtcNow;
            ticket.PrintedByUserId = userId;
        }
        else
        {
            ticket.ReprintCount++;
            ticket.LastReprintAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var newValue = new
        {
            ticket.Id,
            ticket.TicketNumber,
            ticket.IsPrinted,
            ticket.PrintedAt,
            ticket.PrintedByUserId,
            ticket.ReprintCount,
            ticket.LastReprintAt
        };

        return new TicketPrintResult(action, oldValue, newValue);
    }
    public async Task<List<ReservationPassengerTrip>> GetPassengerTripsByIdsAsync(List<Guid> passengerTripIds)
    {
        var tenantId = _tenant.GetTenantId();

        if (!passengerTripIds.Any())
            return [];

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.ReservationPassengerTrips
            .AsNoTracking()
            .Include(x => x.Reservation)
            .Include(x => x.Customer)
            .Include(x => x.BoatRouteSchedule)
            .Where(x =>
                x.TenantId == tenantId &&
                passengerTripIds.Contains(x.Id) &&
                x.Status != "Cancelled" &&
                x.Reservation.Status != "Cancelled")
            .ToListAsync();
    }

    public async Task<BoatDailyTrip?> GetBoatDailyTripByScheduleAndDateAsync(
        Guid boatRouteScheduleId,
        DateOnly tripDate)
    {
        var tenantId = _tenant.GetTenantId();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.BoatDailyTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.BoatRouteScheduleId == boatRouteScheduleId &&
                x.TripDate == tripDate);
    }
}

public sealed record TicketPrintResult(
    string Action,
    object OldValue,
    object NewValue);