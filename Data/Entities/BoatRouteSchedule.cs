using CheckTrip.Web.Data.Entities;

public class BoatRouteSchedule : BaseEntity
{
    public Guid BoatId { get; set; }
    public Boat Boat { get; set; } = default!;

    public Guid RouteId { get; set; }
    public TripRoute Route { get; set; } = default!;

    public Guid ScheduleId { get; set; }
    public Schedule Schedule { get; set; } = default!;

    public decimal Price { get; set; }
    public string? Color { get; set; }
    public string? Days { get; set; }

    public bool IsActive { get; set; } = true;
}