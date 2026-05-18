using CheckTrip.Web.Data.Entities;

public class BoatDailyTrip : BaseEntity
{
    public Guid BoatRouteScheduleId { get; set; }
    public BoatRouteSchedule BoatRouteSchedule { get; set; } = default!;

    public Guid BoatId { get; set; }
    public Boat Boat { get; set; } = default!;

    public Guid RouteId { get; set; }
    public TripRoute Route { get; set; } = default!;

    public Guid ScheduleId { get; set; }
    public Schedule Schedule { get; set; } = default!;

    public DateOnly TripDate { get; set; }

    public string Status { get; set; } = "Open";
    // Open / DocumentGenerated / Closed

    public DateTime? DocumentGeneratedAt { get; set; }
    public Guid? DocumentGeneratedByUserId { get; set; }

    public string? DocumentNumber { get; set; }
    public string? DocumentPath { get; set; }

    public BoatDailyTripCrew? Crew { get; set; }

    public List<BoatDailyTripComment> Comments { get; set; } = [];
}