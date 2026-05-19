using CheckAccess.Infrastructure.Auth;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Features.Tickets.Models;
using CheckTrip.Web.Infrastructure.Audit;
using CheckTrip.Web.Infrastructure.Auth;
using CheckTrip.Web.Infrastructure.Repositories;

namespace CheckTrip.Web.Features.Tickets.Services;

public class TicketService
{
    private readonly TicketRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public TicketService(
        TicketRepository repo,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _repo = repo;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<TicketListItem>> GetByReservationAsync(Guid reservationId)
    {
        var trips = await _repo.GetReservationPassengerTripsAsync(reservationId);
        var tripIds = trips.Select(x => x.Id).ToList();
        var tickets = await _repo.GetTicketsByPassengerTripIdsAsync(tripIds);

        var ticketByTripId = tickets
            .Where(x => x.ReservationPassengerTripId.HasValue)
            .GroupBy(x => x.ReservationPassengerTripId!.Value)
            .ToDictionary(x => x.Key, x => x.First());

        return trips.Select(trip =>
        {
            ticketByTripId.TryGetValue(trip.Id, out var ticket);

            return new TicketListItem
            {
                Id = ticket?.Id ?? Guid.Empty,
                ReservationItemId = trip.Id,
                TicketNumber = ticket?.TicketNumber ?? "Pendiente",
                PassengerName = GetPassengerName(trip),
                DocumentNumber = GetDocumentNumber(trip),
                TripType = GetSegmentText(trip.SegmentType),
                IsGeneric = !trip.CustomerId.HasValue,
                IsPrinted = ticket?.IsPrinted ?? false,
                PrintedAt = ticket?.PrintedAt,
                ReprintCount = ticket?.ReprintCount ?? 0
            };
        }).ToList();
    }

    public async Task GenerateForReservationAsync(Guid reservationId)
    {
        var reservation = await _repo.GetReservationWithPassengerTripsAsync(reservationId);

        if (reservation is null)
            throw new Exception("Reserva no encontrada.");

        if (reservation.Status == "Cancelled")
            throw new Exception("No se pueden generar boletos para una reserva cancelada.");

        var sequence = await _repo.CountTicketsTodayAsync();
        var created = new List<Ticket>();

        foreach (var trip in reservation.PassengerTrips.Where(x => x.Status != "Cancelled"))
        {
            var locked = await _repo.IsTripLockedAsync(trip.BoatRouteScheduleId, trip.TravelDate);

            if (locked)
                throw new Exception($"El viaje del {trip.TravelDate:dd/MM/yyyy} ya tiene documento generado o está cerrado.");

            var exists = await _repo.ExistsForReservationPassengerTripAsync(trip.Id);

            if (exists)
                continue;

            sequence++;

            var ticket = new Ticket
            {
                TenantId = reservation.TenantId,
                ReservationPassengerTripId = trip.Id,
                BoatId = trip.BoatRouteSchedule.BoatId,
                TripDate = trip.TravelDate,
                TicketNumber = GenerateTicketNumber(sequence),
                TicketType = trip.SegmentType,
                GenericPassengerName = trip.GenericPassengerName,
                GenericDocumentNumber = trip.GenericDocumentNumber,
                IsPrinted = false,
                CreatedAt = DateTime.UtcNow
            };

            created.Add(ticket);
        }

        if (created.Any())
            await _repo.AddRangeAsync(created);

        await _audit.LogAsync("Ticket", "Generate", null, new
        {
            ReservationId = reservationId,
            Tickets = created.Select(x => x.TicketNumber).ToList()
        });
    }

    public async Task MarkPrintedAsync(Guid ticketId)
    {
        if (ticketId == Guid.Empty)
            throw new Exception("Primero debe generar el boleto.");

        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var result = await _repo.MarkPrintedAsync(ticketId, user.UserId);

        if (result is null)
            throw new Exception("Boleto no encontrado.");

        await _audit.LogAsync("Ticket", result.Action, result.OldValue, result.NewValue);
    }

    public async Task<List<TicketListItem>> GetByBoatAndDateAsync(Guid boatId, DateTime tripDate)
    {
        return await GetMonitorByBoatAndDateAsync(boatId, tripDate);
    }

    public async Task<List<TicketListItem>> GetMonitorByBoatAndDateAsync(Guid boatId, DateTime tripDate)
    {
        var dateOnly = DateOnly.FromDateTime(tripDate.Date);

        var passengerTrips = await _repo.GetMonitorPassengerTripsByBoatAndDateAsync(boatId, dateOnly);
        var tickets = await _repo.GetTicketsByBoatAndDateAsync(boatId, dateOnly);

        return BuildMonitorResult(passengerTrips, tickets);
    }

    public async Task<List<TicketListItem>> GetMonitorByScheduleAndDateAsync(Guid boatRouteScheduleId, DateTime tripDate)
    {
        var dateOnly = DateOnly.FromDateTime(tripDate.Date);

        var schedule = await _repo.GetBoatRouteScheduleAsync(boatRouteScheduleId);

        if (schedule is null)
            throw new Exception("La ruta/horario seleccionado no existe.");

        var passengerTrips = await _repo.GetMonitorPassengerTripsByScheduleAndDateAsync(
            boatRouteScheduleId,
            dateOnly);

        var tickets = await _repo.GetTicketsByBoatAndDateAsync(
            schedule.BoatId,
            dateOnly);

        return BuildMonitorResult(passengerTrips, tickets);
    }

    public async Task GenerateForBoatAndDateAsync(Guid boatId, DateTime tripDate)
    {
        var scheduleIds = await _repo.GetActiveScheduleIdsByBoatAsync(boatId);

        foreach (var scheduleId in scheduleIds)
            await GenerateForScheduleAndDateAsync(scheduleId, tripDate);
    }

    public async Task GenerateForScheduleAndDateAsync(Guid boatRouteScheduleId, DateTime tripDate)
    {
        var dateOnly = DateOnly.FromDateTime(tripDate.Date);

        var schedule = await _repo.GetBoatRouteScheduleAsync(boatRouteScheduleId);

        if (schedule is null || !schedule.IsActive)
            throw new Exception("La ruta/horario seleccionado no existe o está inactivo.");

        var boatDailyTrip = await _repo.GetBoatDailyTripByScheduleAndDateAsync(
            boatRouteScheduleId,
            dateOnly);

        var trips = await _repo.GetMonitorPassengerTripsByScheduleAndDateAsync(
            boatRouteScheduleId,
            dateOnly);

        var sequence = await _repo.CountTicketsTodayAsync();
        var created = new List<Ticket>();

        foreach (var trip in trips)
        {
            var exists = await _repo.ExistsForReservationPassengerTripAsync(trip.Id);

            if (exists)
                continue;

            sequence++;

            created.Add(new Ticket
            {
                TenantId = trip.TenantId,
                ReservationPassengerTripId = trip.Id,
                BoatDailyTripId = boatDailyTrip?.Id,
                BoatId = schedule.BoatId,
                TripDate = dateOnly,
                TicketNumber = GenerateTicketNumber(sequence),
                TicketType = trip.SegmentType,
                GenericPassengerName = trip.GenericPassengerName,
                GenericDocumentNumber = trip.GenericDocumentNumber,
                IsPrinted = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (created.Any())
            await _repo.AddRangeAsync(created);

        await _audit.LogAsync("Ticket", "GenerateByScheduleAndDate", null, new
        {
            BoatRouteScheduleId = boatRouteScheduleId,
            schedule.BoatId,
            BoatDailyTripId = boatDailyTrip?.Id,
            TripDate = dateOnly,
            Tickets = created.Select(x => x.TicketNumber).ToList()
        });
    }

    private static List<TicketListItem> BuildMonitorResult(
        List<ReservationPassengerTrip> passengerTrips,
        List<Ticket> tickets)
    {
        var ticketByTripId = tickets
            .Where(x => x.ReservationPassengerTripId.HasValue)
            .GroupBy(x => x.ReservationPassengerTripId!.Value)
            .ToDictionary(x => x.Key, x => x.First());

        var result = new List<TicketListItem>();

        foreach (var trip in passengerTrips)
        {
            if (ticketByTripId.TryGetValue(trip.Id, out var ticket))
            {
                result.Add(new TicketListItem
                {
                    Id = ticket.Id,
                    ReservationItemId = trip.Id,
                    TicketNumber = ticket.TicketNumber,
                    PassengerName = GetPassengerName(trip),
                    DocumentNumber = GetDocumentNumber(trip),
                    TripType = GetSegmentText(ticket.TicketType ?? trip.SegmentType),
                    IsGeneric = false,
                    IsPrinted = ticket.IsPrinted,
                    PrintedAt = ticket.PrintedAt,
                    ReprintCount = ticket.ReprintCount
                });
            }
            else
            {
                result.Add(new TicketListItem
                {
                    Id = Guid.Empty,
                    ReservationItemId = trip.Id,
                    TicketNumber = "Pendiente",
                    PassengerName = GetPassengerName(trip),
                    DocumentNumber = GetDocumentNumber(trip),
                    TripType = GetSegmentText(trip.SegmentType),
                    IsGeneric = false,
                    IsPrinted = false,
                    PrintedAt = null,
                    ReprintCount = 0
                });
            }
        }

        var genericTickets = tickets
            .Where(x => !x.ReservationPassengerTripId.HasValue)
            .Select(x => new TicketListItem
            {
                Id = x.Id,
                ReservationItemId = null,
                TicketNumber = x.TicketNumber,
                PassengerName = x.GenericPassengerName ?? "Ticket genérico",
                DocumentNumber = x.GenericDocumentNumber ?? "",
                TripType = x.TicketType ?? "Genérico",
                IsGeneric = true,
                IsPrinted = x.IsPrinted,
                PrintedAt = x.PrintedAt,
                ReprintCount = x.ReprintCount
            });

        result.AddRange(genericTickets);

        return result
            .OrderBy(x => x.TicketNumber == "Pendiente" ? 1 : 0)
            .ThenBy(x => x.TripType)
            .ThenBy(x => x.PassengerName)
            .ToList();
    }

    private static string GetPassengerName(ReservationPassengerTrip trip)
    {
        return trip.CustomerId.HasValue
            ? trip.Customer?.FullName ?? "Sin nombre"
            : trip.GenericPassengerName ?? "Pasajero genérico";
    }

    private static string GetDocumentNumber(ReservationPassengerTrip trip)
    {
        return trip.CustomerId.HasValue
            ? trip.Customer?.DocumentNumber ?? ""
            : trip.GenericDocumentNumber ?? "";
    }

    private static string GetSegmentText(string? segmentType)
    {
        return segmentType switch
        {
            "Outbound" => "Ida",
            "Return" => "Retorno",
            _ => segmentType ?? string.Empty
        };
    }

    private static string GenerateTicketNumber(int sequence)
    {
        return $"TKT-{DateTime.UtcNow:yyyyMMdd}-{sequence:0000}";
    }

    public async Task<List<TicketGenerateResult>> GenerateForPassengerTripIdsAsync(List<Guid> passengerTripIds)
    {
        var result = new List<TicketGenerateResult>();

        if (!passengerTripIds.Any())
            return result;

        var trips = await _repo.GetPassengerTripsByIdsAsync(passengerTripIds);
        var sequence = await _repo.CountTicketsTodayAsync();

        foreach (var tripId in passengerTripIds)
        {
            var item = new TicketGenerateResult
            {
                ReservationPassengerTripId = tripId
            };

            try
            {
                var trip = trips.FirstOrDefault(x => x.Id == tripId);

                if (trip is null)
                    throw new Exception("Pasajero no encontrado o cancelado.");

                var exists = await _repo.ExistsForReservationPassengerTripAsync(trip.Id);

                if (exists)
                    throw new Exception("El boleto ya existe.");

                var schedule = trip.BoatRouteSchedule;

                var boatDailyTrip = await _repo.GetBoatDailyTripByScheduleAndDateAsync(
                    trip.BoatRouteScheduleId,
                    trip.TravelDate);

                sequence++;

                var ticket = new Ticket
                {
                    TenantId = trip.TenantId,
                    ReservationPassengerTripId = trip.Id,
                    BoatDailyTripId = boatDailyTrip?.Id,
                    BoatId = schedule.BoatId,
                    TripDate = trip.TravelDate,
                    TicketNumber = GenerateTicketNumber(sequence),
                    TicketType = trip.SegmentType,
                    GenericPassengerName = trip.GenericPassengerName,
                    GenericDocumentNumber = trip.GenericDocumentNumber,
                    IsPrinted = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _repo.AddAsync(ticket);

                item.TicketId = ticket.Id;
                item.TicketNumber = ticket.TicketNumber;
                item.Success = true;
            }
            catch (Exception ex)
            {
                item.Success = false;
                item.Error = ex.Message;
            }

            result.Add(item);
        }

        await _audit.LogAsync("Ticket", "GenerateSelected", null, new
        {
            PassengerTripIds = passengerTripIds,
            Results = result
        });

        return result;
    }
    public async Task CreateGenericTicketAsync(
    Guid boatId,
    DateTime tripDate,
    string passengerName,
    string? documentNumber,
    string ticketType,
    Guid? boatRouteScheduleId = null)
    {
        var dateOnly = DateOnly.FromDateTime(tripDate.Date);

        BoatDailyTrip? boatDailyTrip = null;

        if (boatRouteScheduleId.HasValue)
        {
            boatDailyTrip = await _repo.GetBoatDailyTripByScheduleAndDateAsync(
                boatRouteScheduleId.Value,
                dateOnly);
        }

        var sequence = await _repo.CountTicketsTodayAsync() + 1;

        var ticket = new Ticket
        {
            TenantId = await _repo.GetTenantIdAsync(),
            ReservationPassengerTripId = null,
            BoatDailyTripId = boatDailyTrip?.Id,
            BoatId = boatId,
            TripDate = dateOnly,
            TicketNumber = GenerateTicketNumber(sequence),
            TicketType = ticketType,
            GenericPassengerName = passengerName,
            GenericDocumentNumber = documentNumber,
            IsPrinted = false,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(ticket);

        await _audit.LogAsync("Ticket", "CreateGeneric", null, new
        {
            ticket.Id,
            ticket.TicketNumber,
            ticket.BoatDailyTripId,
            ticket.BoatId,
            ticket.TripDate,
            ticket.GenericPassengerName,
            ticket.GenericDocumentNumber,
            ticket.TicketType
        });
    }
}