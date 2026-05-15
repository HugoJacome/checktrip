using CheckAccess.Infrastructure.Auth;
using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Features.Tickets.Models;
using CheckTrip.Web.Infrastructure.Audit;
using CheckTrip.Web.Infrastructure.Auth;
using CheckTrip.Web.Infrastructure.Repositories;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Features.Tickets.Services;

public class TicketService
{
    private readonly AppDbContext _db;
    private readonly TicketRepository _repo;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public TicketService(
        AppDbContext db,
        TicketRepository repo,
        ITenantProvider tenant,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _db = db;
        _repo = repo;
        _tenant = tenant;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<TicketListItem>> GetByReservationAsync(Guid reservationId)
    {
        var tickets = await _repo.GetByReservationAsync(reservationId);

        return tickets.Select(x => new TicketListItem
        {
            Id = x.Id,
            ReservationItemId = x.ReservationItemId,
            TicketNumber = x.TicketNumber,
            PassengerName = x.ReservationItem.Customer.FullName,
            DocumentNumber = x.ReservationItem.Customer.DocumentNumber,
            TripType = x.ReservationItem.TripType,
            IsPrinted = x.IsPrinted,
            PrintedAt = x.PrintedAt,
            ReprintCount = x.ReprintCount
        }).ToList();
    }

    public async Task GenerateForReservationAsync(Guid reservationId)
    {
        var tenantId = _tenant.GetTenantId();

        var reservation = await _db.Reservations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == reservationId &&
                x.TenantId == tenantId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        if (reservation.Status == "Cancelled")
            throw new Exception("No se pueden generar boletos para una reserva cancelada.");

        var created = new List<Ticket>();

        foreach (var item in reservation.Items.Where(x => x.Status != "Cancelled"))
        {
            var exists = await _repo.ExistsForReservationItemAsync(item.Id);

            if (exists)
                continue;

            var ticket = new Ticket
            {
                TenantId = tenantId,
                ReservationItemId = item.Id,
                TicketNumber = await GenerateTicketNumberAsync(),
                TicketType = item.TripType,
                Color = null,
                IsPrinted = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.Tickets.Add(ticket);
            created.Add(ticket);
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Ticket", "Generate", null, new
        {
            ReservationId = reservationId,
            Tickets = created.Select(x => x.TicketNumber).ToList()
        });
    }

    public async Task MarkPrintedAsync(Guid ticketId)
    {
        var user = await _currentUser.LoadAsync();
        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var ticket = await _repo.GetAsync(ticketId);

        if (ticket is null)
            throw new Exception("Boleto no encontrado.");

        var old = new
        {
            ticket.IsPrinted,
            ticket.PrintedAt,
            ticket.ReprintCount
        };

        if (!ticket.IsPrinted)
        {
            ticket.IsPrinted = true;
            ticket.PrintedAt = DateTime.UtcNow;
            ticket.PrintedByUserId = user.UserId;
        }
        else
        {
            ticket.ReprintCount++;
            ticket.LastReprintAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Ticket", ticket.ReprintCount > 0 ? "Reprint" : "Print", old, ticket);
    }

    public async Task<List<TicketListItem>> GetByBoatAndDateAsync(Guid boatId, DateTime tripDate)
    {
        var tickets = await _repo.GetByBoatAndDateAsync(boatId, tripDate);

        return tickets.Select(x => new TicketListItem
        {
            Id = x.Id,
            ReservationItemId = x.ReservationItemId,
            TicketNumber = x.TicketNumber,
            PassengerName = x.ReservationItem?.Customer?.FullName ?? x.GenericPassengerName ?? "Ticket genérico",
            DocumentNumber = x.ReservationItem?.Customer?.DocumentNumber ?? x.GenericDocumentNumber ?? "",
            TripType = x.TicketType ?? "",
            IsGeneric = x.ReservationItemId == null,
            IsPrinted = x.IsPrinted,
            PrintedAt = x.PrintedAt,
            ReprintCount = x.ReprintCount
        }).ToList();
    }

    public async Task GenerateForBoatAndDateAsync(Guid boatId, DateTime tripDate)
    {
        var tenantId = _tenant.GetTenantId();

        var tripDateUtc = DateTime.SpecifyKind(tripDate.Date, DateTimeKind.Utc);
        var dateOnly = DateOnly.FromDateTime(tripDateUtc);

        var items = await _db.ReservationItems
            .Include(x => x.Reservation)
            .Where(x =>
                x.TenantId == tenantId &&
                x.Status != "Cancelled" &&
                x.Reservation.Status != "Cancelled" &&
                (
                    (x.OutboundTravelDate == dateOnly &&
                     x.OutboundRouteSchedule != null &&
                     x.OutboundRouteSchedule.BoatId == boatId)
                    ||
                    (x.ReturnTravelDate == dateOnly &&
                     x.ReturnRouteSchedule != null &&
                     x.ReturnRouteSchedule.BoatId == boatId)
                ))
            .ToListAsync();

        foreach (var item in items)
        {
            var exists = await _repo.ExistsForReservationItemAsync(item.Id);

            if (exists)
                continue;

            var ticket = new Ticket
            {
                TenantId = tenantId,
                ReservationItemId = item.Id,
                BoatId = boatId,
                TripDate = tripDateUtc,
                TicketNumber = await GenerateTicketNumberAsync(),
                TicketType = item.TripType,
                IsPrinted = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.Tickets.Add(ticket);
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Ticket", "GenerateByBoatAndDate", null, new
        {
            BoatId = boatId,
            TripDate = tripDateUtc
        });
    }

    public async Task CreateGenericTicketAsync(
        Guid boatId,
        DateTime tripDate,
        string passengerName,
        string? documentNumber,
        string ticketType)
    {
        var tenantId = _tenant.GetTenantId();

        var ticket = new Ticket
        {
            TenantId = tenantId,
            ReservationItemId = null,
            BoatId = boatId,
            TripDate = tripDate.Date,
            TicketNumber = await GenerateTicketNumberAsync(),
            TicketType = ticketType,
            GenericPassengerName = passengerName,
            GenericDocumentNumber = documentNumber,
            IsPrinted = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Ticket", "CreateGeneric", null, ticket);
    }
    private async Task<string> GenerateTicketNumberAsync()
    {
        var count = await _repo.CountTicketsTodayAsync() + 1;
        return $"TKT-{DateTime.UtcNow:yyyyMMdd}-{count:0000}";
    }
}