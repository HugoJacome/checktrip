using CheckAccess.Infrastructure.Auth;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Features.Reservations.Models;
using CheckTrip.Web.Features.Reservations.Models.Operations;
using CheckTrip.Web.Features.Tickets.Models;
using CheckTrip.Web.Infrastructure.Audit;
using CheckTrip.Web.Infrastructure.Repositories;

namespace CheckTrip.Web.Features.Tickets.Services;

public class BoatDailyTripService
{
    private readonly BoatDailyTripRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public BoatDailyTripService(
        BoatDailyTripRepository repo,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _repo = repo;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<RouteScheduleCatalogItem>> GetRouteSchedulesByBoatAsync(Guid boatId)
    {
        return await _repo.GetRouteSchedulesByBoatAsync(boatId);
    }

    public async Task<BoatDailyTripResult> GetOrCreateAsync(Guid boatRouteScheduleId, DateTime tripDate)
    {
        var date = DateOnly.FromDateTime(tripDate.Date);

        var schedule = await _repo.GetRouteScheduleDetailAsync(boatRouteScheduleId);

        if (schedule is null)
            throw new Exception("La ruta/horario seleccionado no existe o está inactivo.");

        var trip = await _repo.GetByScheduleAndDateAsync(boatRouteScheduleId, date);

        if (trip is null)
        {
            return new BoatDailyTripResult
            {
                BoatId = schedule.BoatId,
                TripDate = tripDate.Date,
                Status = BoatDailyTripStatus.Open
            };
        }

        return new BoatDailyTripResult
        {
            Id = trip.Id,
            BoatId = trip.BoatId,
            TripDate = trip.TripDate.ToDateTime(TimeOnly.MinValue),
            Status = trip.Status,
            DocumentGeneratedAt = trip.DocumentGeneratedAt,
            DocumentNumber = trip.DocumentNumber,
            DocumentPath = trip.DocumentPath
        };
    }

    public async Task<BoatDailyTripResult> GetOrCreateByBoatAsync(Guid boatId, DateTime tripDate)
    {
        var schedule = await _repo.GetFirstScheduleByBoatAsync(boatId);

        if (schedule is null)
            throw new Exception("El bote no tiene rutas/horarios activos configurados.");

        return await GetOrCreateAsync(schedule.Id, tripDate);
    }

    public async Task<bool> IsLockedAsync(Guid boatRouteScheduleId, DateTime tripDate)
    {
        var date = DateOnly.FromDateTime(tripDate.Date);
        return await _repo.IsLockedAsync(boatRouteScheduleId, date);
    }

    public async Task GenerateDocumentAsync(Guid boatRouteScheduleId, DateTime tripDate)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var schedule = await _repo.GetRouteScheduleDetailAsync(boatRouteScheduleId);

        if (schedule is null)
            throw new Exception("La ruta/horario seleccionado no existe o está inactivo.");

        var date = DateOnly.FromDateTime(tripDate.Date);
        var documentNumber = $"DOC-{tripDate:yyyyMMdd}-{schedule.BoatId.ToString()[..8]}";

        var trip = await _repo.GenerateDocumentAsync(
            schedule,
            date,
            user.UserId,
            documentNumber);

        await _audit.LogAsync("BoatDailyTrip", "GenerateDocument", null, new
        {
            trip.Id,
            BoatRouteScheduleId = boatRouteScheduleId,
            schedule.BoatId,
            schedule.RouteId,
            schedule.ScheduleId,
            TripDate = date,
            trip.DocumentNumber
        });
    }

    public async Task CloseAsync(Guid boatRouteScheduleId, DateTime tripDate)
    {
        var date = DateOnly.FromDateTime(tripDate.Date);

        var trip = await _repo.CloseByScheduleAndDateAsync(boatRouteScheduleId, date);

        await _audit.LogAsync("BoatDailyTrip", "Close", null, new
        {
            trip.Id,
            BoatRouteScheduleId = boatRouteScheduleId,
            TripDate = date
        });
    }

    public async Task CloseByIdAsync(Guid boatDailyTripId)
    {
        var trip = await _repo.CloseAsync(boatDailyTripId);

        await _audit.LogAsync("BoatDailyTrip", "Close", null, new
        {
            trip.Id,
            trip.BoatRouteScheduleId,
            trip.TripDate
        });
    }

    public async Task SaveCrewAsync(FinishBoatDailyTripModel model)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        if (model.BoatDailyTripId == Guid.Empty)
            throw new Exception("Debe seleccionar un viaje.");

        if (string.IsNullOrWhiteSpace(model.CaptainName))
            throw new Exception("Debe ingresar el nombre del capitán.");

        var crew = new BoatDailyTripCrew
        {
            BoatDailyTripId = model.BoatDailyTripId,
            CaptainName = model.CaptainName.Trim(),
            CaptainDocument = model.CaptainDocument?.Trim(),
            Sailor1Name = model.Sailor1Name?.Trim(),
            Sailor1Document = model.Sailor1Document?.Trim(),
            Sailor2Name = model.Sailor2Name?.Trim(),
            Sailor2Document = model.Sailor2Document?.Trim(),
            Sailor3Name = model.Sailor3Name?.Trim(),
            Sailor3Document = model.Sailor3Document?.Trim(),
            Notes = model.Notes?.Trim(),
            CreatedByUserId = user.UserId
        };

        await _repo.SaveCrewAsync(model.BoatDailyTripId, crew);

        await _audit.LogAsync("BoatDailyTrip", "SaveCrew", null, new
        {
            model.BoatDailyTripId,
            crew.CaptainName,
            crew.CaptainDocument,
            crew.Sailor1Name,
            crew.Sailor2Name,
            crew.Sailor3Name,
            crew.Notes
        });
    }

    public async Task AddCommentAsync(AddBoatDailyTripCommentModel model)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        if (model.BoatDailyTripId == Guid.Empty)
            throw new Exception("Debe seleccionar un viaje.");

        if (string.IsNullOrWhiteSpace(model.Comment))
            throw new Exception("Debe ingresar un comentario.");

        var comment = new BoatDailyTripComment
        {
            BoatDailyTripId = model.BoatDailyTripId,
            CommentType = string.IsNullOrWhiteSpace(model.CommentType)
                ? "General"
                : model.CommentType.Trim(),
            Comment = model.Comment.Trim(),
            CreatedByUserId = user.UserId
        };

        await _repo.AddCommentAsync(comment);

        await _audit.LogAsync("BoatDailyTrip", "AddComment", null, new
        {
            model.BoatDailyTripId,
            comment.CommentType,
            comment.Comment
        });
    }

    public async Task<List<BoatDailyTripOperationItem>> GetOperationsAsync(BoatDailyTripOperationsFilter filter)
    {
        var trips = await _repo.GetOperationsAsync(filter);

        var result = new List<BoatDailyTripOperationItem>();

        foreach (var trip in trips)
        {
            var passengerCount = await _repo.CountPassengerTripsByBoatDailyTripAsync(trip.Id);

            result.Add(new BoatDailyTripOperationItem
            {
                Id = trip.Id,
                BoatRouteScheduleId = trip.BoatRouteScheduleId,
                BoatId = trip.BoatId,
                RouteId = trip.RouteId,
                ScheduleId = trip.ScheduleId,
                TripDate = trip.TripDate,
                Boat = trip.Boat.Name,
                Route = $"{trip.Route.Origin} - {trip.Route.Destination}",
                Schedule = trip.Schedule.Name,
                CaptainName = trip.Crew?.CaptainName,
                Sailors = BuildSailorsText(trip.Crew),
                PassengerCount = passengerCount,
                Capacity = trip.Boat.Capacity,
                ExtraCapacity = trip.Boat.ExtraCapacity,
                Status = trip.Status,
                DocumentNumber = trip.DocumentNumber,
                DocumentPath = trip.DocumentPath,
                DocumentGeneratedAt = trip.DocumentGeneratedAt,
                LastComment = trip.Comments
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => x.Comment)
                    .FirstOrDefault(),
                CreatedAt = trip.CreatedAt
            });
        }

        return result;
    }

    public async Task<List<BoatDailyTripPassengerOperationItem>> GetPassengersAsync(Guid boatDailyTripId)
    {
        var trip = await _repo.GetByIdAsync(boatDailyTripId);

        if (trip is null)
            throw new Exception("Viaje no encontrado.");

        var passengerTrips = await _repo.GetPassengerTripsByBoatDailyTripAsync(boatDailyTripId);

        return BuildPassengerOperationItems(trip.Id, passengerTrips);
    }

    public async Task<List<BoatDailyTripPassengerOperationItem>> GetPassengersForOperationsAsync(
        BoatDailyTripOperationsFilter filter)
    {
        var passengerTrips = await _repo.GetPassengerTripsForOperationsAsync(filter);

        return BuildPassengerOperationItems(null, passengerTrips);
    }

    private static List<BoatDailyTripPassengerOperationItem> BuildPassengerOperationItems(
        Guid? boatDailyTripId,
        List<ReservationPassengerTrip> passengerTrips)
    {
        return passengerTrips
            .GroupBy(GetPassengerOperationGroupKey)
            .Select(g =>
            {
                var outbound = g.FirstOrDefault(x => x.SegmentType == "Outbound");
                var ret = g.FirstOrDefault(x => x.SegmentType == "Return");
                var first = outbound ?? ret ?? g.First();

                return new BoatDailyTripPassengerOperationItem
                {
                    BoatDailyTripId = boatDailyTripId ?? Guid.Empty,

                    OutboundPassengerTripId = outbound?.Id,
                    ReturnPassengerTripId = ret?.Id,

                    ReservationId = first.ReservationId,
                    ReservationCode = first.Reservation.ReservationCode,
                    ExternalReference = first.Reservation.ExternalReference,
                    Agency = first.Reservation.Agency?.Name,
                    Seller = first.Reservation.Seller is null
                        ? null
                        : $"{first.Reservation.Seller.FirstName} {first.Reservation.Seller.LastName}".Trim(),

                    CustomerId = first.CustomerId,
                    DocumentType = first.Customer?.DocumentType ?? "Generico",
                    DocumentNumber = GetPassengerDocument(first),
                    FullName = GetPassengerName(first),
                    Nationality = first.Customer?.Nationality ?? "Ecuatoriana",
                    Age = first.Customer?.Age ?? 18,
                    PassengerType = first.PassengerType,

                    OutboundBoat = outbound?.BoatRouteSchedule.Boat?.Name,
                    OutboundRoute = outbound?.BoatRouteSchedule.Route is null
                        ? null
                        : $"{outbound.BoatRouteSchedule.Route.Origin} - {outbound.BoatRouteSchedule.Route.Destination}",
                    OutboundSchedule = outbound?.BoatRouteSchedule.Schedule?.Name,
                    OutboundTravelDate = outbound?.TravelDate,

                    ReturnBoat = ret?.BoatRouteSchedule.Boat?.Name,
                    ReturnRoute = ret?.BoatRouteSchedule.Route is null
                        ? null
                        : $"{ret.BoatRouteSchedule.Route.Origin} - {ret.BoatRouteSchedule.Route.Destination}",
                    ReturnSchedule = ret?.BoatRouteSchedule.Schedule?.Name,
                    ReturnTravelDate = ret?.TravelDate,

                    ReservationStatus = first.Reservation.Status,
                    PaymentStatus = first.Reservation.PaymentStatus
                };
            })
            .OrderBy(x => x.FullName)
            .ToList();
    }

    private static string GetPassengerOperationGroupKey(ReservationPassengerTrip trip)
    {
        if (trip.CustomerId.HasValue)
            return $"R:{trip.ReservationId}|C:{trip.CustomerId.Value}";

        return $"R:{trip.ReservationId}|G:{trip.GenericDocumentNumber}|{trip.GenericPassengerName}|{trip.PassengerType}";
    }

    private static string GetPassengerDocument(ReservationPassengerTrip trip)
    {
        return trip.Customer?.DocumentNumber
               ?? trip.GenericDocumentNumber
               ?? string.Empty;
    }

    private static string GetPassengerName(ReservationPassengerTrip trip)
    {
        return trip.Customer?.FullName
               ?? trip.GenericPassengerName
               ?? "Pasajero genérico";
    }

    private static string? BuildSailorsText(BoatDailyTripCrew? crew)
    {
        if (crew is null)
            return null;

        var sailors = new[]
        {
        crew.Sailor1Name,
        crew.Sailor2Name,
        crew.Sailor3Name
    }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToList();

        return sailors.Any()
            ? string.Join(", ", sailors)
            : null;
    }
    public async Task ChangeBoatAsync(ChangeBoatDailyTripModel model)
    {
        if (model.BoatDailyTripId == Guid.Empty)
            throw new Exception("Debe seleccionar un viaje.");

        if (model.NewBoatRouteScheduleId == Guid.Empty)
            throw new Exception("Debe seleccionar la nueva embarcación/ruta/horario.");

        if (string.IsNullOrWhiteSpace(model.Reason))
            throw new Exception("Debe ingresar el motivo del cambio.");

        var currentTrip = await _repo.GetByIdAsync(model.BoatDailyTripId);

        if (currentTrip is null)
            throw new Exception("Viaje no encontrado.");

        //if (currentTrip.Status == BoatDailyTripStatus.DocumentGenerated ||
        //    currentTrip.Status == BoatDailyTripStatus.Closed)
        //    throw new Exception("El viaje ya tiene documento generado o está cerrado.");

        var newSchedule = await _repo.GetRouteScheduleDetailAsync(model.NewBoatRouteScheduleId);

        if (newSchedule is null)
            throw new Exception("La nueva embarcación/ruta/horario no existe o está inactiva.");

        var oldValues = new
        {
            currentTrip.Id,
            currentTrip.BoatRouteScheduleId,
            currentTrip.BoatId,
            Boat = currentTrip.Boat.Name,
            currentTrip.RouteId,
            Route = $"{currentTrip.Route.Origin} - {currentTrip.Route.Destination}",
            currentTrip.ScheduleId,
            Schedule = currentTrip.Schedule.Name,
            currentTrip.TripDate,
            currentTrip.Status
        };

        var updated = await _repo.ChangeBoatRouteScheduleAsync(
            model.BoatDailyTripId,
            newSchedule);

        await _audit.LogAsync("BoatDailyTrip", "ChangeBoat", oldValues, new
        {
            updated.Id,
            updated.BoatRouteScheduleId,
            updated.BoatId,
            Boat = newSchedule.Boat.Name,
            updated.RouteId,
            Route = $"{newSchedule.Route.Origin} - {newSchedule.Route.Destination}",
            updated.ScheduleId,
            Schedule = newSchedule.Schedule.Name,
            updated.TripDate,
            model.Reason
        });
    }
    public async Task<List<RouteScheduleCatalogItem>> GetAllRouteSchedulesAsync()
    {
        return await _repo.GetAllRouteSchedulesAsync();
    }
    public async Task<BoatDailyTrip?> GetByIdAsync(Guid boatDailyTripId)
    {
        return await _repo.GetByIdAsync(boatDailyTripId);
    }
    public async Task<List<CrewMemberCatalogItem>> GetCrewMembersByBoatAsync(Guid boatId)
    {
        return await _repo.GetCrewMembersByBoatAsync(boatId);
    }

    public async Task<GenerateTripDocumentModel> GetGenerateDocumentModelAsync(
        Guid boatId,
        Guid boatRouteScheduleId,
        DateTime tripDate)
    {
        var date = DateOnly.FromDateTime(tripDate.Date);

        var trip = await _repo.GetTripWithCrewByScheduleAndDateAsync(
            boatRouteScheduleId,
            date);

        var model = new GenerateTripDocumentModel
        {
            BoatId = boatId,
            BoatRouteScheduleId = boatRouteScheduleId,
            TripDate = tripDate.Date
        };

        if (trip?.Crew is not null)
        {
            model.CaptainName = trip.Crew.CaptainName;
            model.CaptainDocument = trip.Crew.CaptainDocument;
            model.Sailor1Name = trip.Crew.Sailor1Name;
            model.Sailor1Document = trip.Crew.Sailor1Document;
            model.Sailor2Name = trip.Crew.Sailor2Name;
            model.Sailor2Document = trip.Crew.Sailor2Document;
            model.Sailor3Name = trip.Crew.Sailor3Name;
            model.Sailor3Document = trip.Crew.Sailor3Document;
            model.Notes = trip.Crew.Notes;
        }

        return model;
    }

    public async Task<GenerateTripDocumentResult> GenerateDocumentWithCrewAsync(GenerateTripDocumentModel model)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        if (model.BoatRouteScheduleId == Guid.Empty)
            throw new Exception("Debe seleccionar ruta/horario.");

        if (string.IsNullOrWhiteSpace(model.CaptainName))
            throw new Exception("Debe ingresar el capitán.");

        var date = DateOnly.FromDateTime(model.TripDate.Date);

        var schedule = await _repo.GetRouteScheduleDetailAsync(model.BoatRouteScheduleId);

        if (schedule is null)
            throw new Exception("La ruta/horario seleccionado no existe o está inactivo.");

        var trip = await _repo.GetOrCreateAsync(schedule, date);

        var crew = new BoatDailyTripCrew
        {
            BoatDailyTripId = trip.Id,
            CaptainName = model.CaptainName.Trim(),
            CaptainDocument = model.CaptainDocument?.Trim(),
            Sailor1Name = model.Sailor1Name?.Trim(),
            Sailor1Document = model.Sailor1Document?.Trim(),
            Sailor2Name = model.Sailor2Name?.Trim(),
            Sailor2Document = model.Sailor2Document?.Trim(),
            Sailor3Name = model.Sailor3Name?.Trim(),
            Sailor3Document = model.Sailor3Document?.Trim(),
            Notes = model.Notes?.Trim(),
            CreatedByUserId = user.UserId
        };

        await _repo.SaveCrewAsync(trip.Id, crew);

        var documentNumber = $"DOC-{model.TripDate:yyyyMMdd}-{schedule.BoatId.ToString()[..8]}";

        var updatedTrip = await _repo.GenerateDocumentAsync(
            schedule,
            date,
            user.UserId,
            documentNumber);

        var passengers = await _repo.GetPassengerTripsByBoatDailyTripAsync(updatedTrip.Id);

        var content = await GenerateTripDocumentWordAsync(
            updatedTrip,
            schedule,
            crew,
            passengers);

        var fileName = $"ListaPasajeros-{documentNumber}.docx";

        await _audit.LogAsync("BoatDailyTrip", "GenerateDocumentWithCrew", null, new
        {
            updatedTrip.Id,
            model.BoatId,
            model.BoatRouteScheduleId,
            TripDate = date,
            updatedTrip.DocumentNumber,
            model.CaptainName,
            model.CaptainDocument,
            model.Sailor1Name,
            model.Sailor2Name,
            model.Sailor3Name,
            PassengerCount = passengers.Count
        });

        return new GenerateTripDocumentResult
        {
            DocumentNumber = updatedTrip.DocumentNumber ?? documentNumber,
            FileName = fileName,
            Content = content
        };
    }
    private async Task<byte[]> GenerateTripDocumentWordAsync(
    BoatDailyTrip trip,
    BoatRouteSchedule schedule,
    BoatDailyTripCrew crew,
    List<ReservationPassengerTrip> passengers)
    {
        await Task.CompletedTask;

        return TripDocumentGenerator.GeneratePassengerListDocument(
            trip,
            schedule,
            crew,
            passengers);
    }

    public async Task<BoatDailyTripOperationsReportModel> GetOperationsReportAsync(
    BoatDailyTripOperationsFilter filter)
    {
        var passengerTrips = await _repo.GetPassengerTripsForOperationsAsync(filter);

        var outboundTrips = passengerTrips
            .Where(x => x.SegmentType == "Outbound")
            .ToList();

        decimal GetAgencyAmount(ReservationPassengerTrip x)
        {
            var agencyRate = x.Reservation.Agency?.RouteRates
                .FirstOrDefault(r =>
                    r.RouteId == x.BoatRouteSchedule.RouteId &&
                    r.IsActive);

            return agencyRate?.Price ?? x.TotalPrice;
        }

        decimal GetSellerCommission(ReservationPassengerTrip x)
        {
            var sellerCommission = x.Reservation.Seller?.RouteCommissions
                .FirstOrDefault(r =>
                    r.RouteId == x.BoatRouteSchedule.RouteId &&
                    r.IsActive);

            return sellerCommission?.Commission ?? 0;
        }

        var report = new BoatDailyTripOperationsReportModel
        {
            TotalPassengers = passengerTrips.Count,
            OutboundPassengers = outboundTrips.Count,
            ReturnPassengers = passengerTrips.Count(x => x.SegmentType == "Return"),
            TotalAmount = outboundTrips.Sum(GetAgencyAmount)
        };

        report.ByTrip = passengerTrips
            .GroupBy(x => new
            {
                x.TravelDate,
                Boat = x.BoatRouteSchedule.Boat.Name,
                RouteId = x.BoatRouteSchedule.RouteId,
                Route = x.BoatRouteSchedule.Route.Origin + " - " + x.BoatRouteSchedule.Route.Destination,
                Schedule = x.BoatRouteSchedule.Schedule.Name
            })
            .Select(g => new BoatDailyTripReportByTripItem
            {
                TravelDate = g.Key.TravelDate,
                Boat = g.Key.Boat,
                Route = g.Key.Route,
                Schedule = g.Key.Schedule,
                OutboundPassengers = g.Count(x => x.SegmentType == "Outbound"),
                ReturnPassengers = g.Count(x => x.SegmentType == "Return"),
                TotalPassengers = g.Count(),
                TotalAmount = g
                    .Where(x => x.SegmentType == "Outbound")
                    .Sum(GetAgencyAmount)
            })
            .OrderBy(x => x.TravelDate)
            .ThenBy(x => x.Boat)
            .ThenBy(x => x.Schedule)
            .ToList();

        report.ByAgency = passengerTrips
            .GroupBy(x => x.Reservation.Agency?.Name ?? "Sin agencia")
            .Select(g => new BoatDailyTripReportByAgencyItem
            {
                Agency = g.Key,
                OutboundPassengers = g.Count(x => x.SegmentType == "Outbound"),
                ReturnPassengers = g.Count(x => x.SegmentType == "Return"),
                TotalPassengers = g.Count(),
                TotalAmount = g
                    .Where(x => x.SegmentType == "Outbound")
                    .Sum(GetAgencyAmount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ThenBy(x => x.Agency)
            .ToList();

        report.BySeller = passengerTrips
            .GroupBy(x => new
            {
                Seller = x.Reservation.Seller is null
                    ? "Sin vendedor"
                    : $"{x.Reservation.Seller.FirstName} {x.Reservation.Seller.LastName}".Trim(),

                RouteId = x.BoatRouteSchedule.RouteId,
                Route = x.BoatRouteSchedule.Route.Origin + " - " + x.BoatRouteSchedule.Route.Destination
            })
            .Select(g => new BoatDailyTripReportBySellerItem
            {
                Seller = g.Key.Seller,
                Route = g.Key.Route,
                OutboundPassengers = g.Count(x => x.SegmentType == "Outbound"),
                ReturnPassengers = g.Count(x => x.SegmentType == "Return"),
                TotalPassengers = g.Count(),
                TotalAmount = g
                    .Where(x => x.SegmentType == "Outbound")
                    .Sum(GetSellerCommission)
            })
            .OrderBy(x => x.Seller)
            .ThenBy(x => x.Route)
            .ToList();

        return report;
    }
}