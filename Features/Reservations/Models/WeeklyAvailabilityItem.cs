namespace CheckTrip.Web.Features.Reservations.Models;

public class WeeklyAvailabilityItem
{
    public Guid BoatRouteScheduleId { get; set; }
    public Guid BoatId { get; set; }

    public string Boat { get; set; } = default!;
    public string Route { get; set; } = default!;
    public string Schedule { get; set; } = default!;

    public DateTime TravelDate { get; set; }

    public int Capacity { get; set; }
    public int ExtraCapacity { get; set; }
    public int Reserved { get; set; }

    public int TotalCapacity => Capacity + ExtraCapacity;
    public int Available => TotalCapacity - Reserved;

    public bool IsFull => Available <= 0;
}