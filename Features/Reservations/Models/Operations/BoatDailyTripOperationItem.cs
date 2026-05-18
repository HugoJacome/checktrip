namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class BoatDailyTripOperationItem
{
    public Guid Id { get; set; }

    public Guid BoatRouteScheduleId { get; set; }
    public Guid BoatId { get; set; }
    public Guid RouteId { get; set; }
    public Guid ScheduleId { get; set; }

    public DateOnly TripDate { get; set; }

    public string Boat { get; set; } = default!;
    public string Route { get; set; } = default!;
    public string Schedule { get; set; } = default!;

    public string? CaptainName { get; set; }
    public string? Sailors { get; set; }

    public int PassengerCount { get; set; }
    public int Capacity { get; set; }
    public int ExtraCapacity { get; set; }
    public int TotalCapacity => Capacity + ExtraCapacity;

    public string Status { get; set; } = default!;

    public bool HasDocument => !string.IsNullOrWhiteSpace(DocumentNumber);
    public string? DocumentNumber { get; set; }
    public string? DocumentPath { get; set; }
    public DateTime? DocumentGeneratedAt { get; set; }

    public string? LastComment { get; set; }

    public bool IsLocked =>
        Status == "DocumentGenerated" ||
        Status == "Closed";

    public DateTime CreatedAt { get; set; }
}