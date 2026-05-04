namespace CheckTrip.Web.Features.Reservations.Models;

public class AvailabilityResult
{
    public Guid BoatRouteScheduleId { get; set; }
    public DateTime TravelDate { get; set; }

    public int Capacity { get; set; }
    public int ExtraCapacity { get; set; }
    public int TotalCapacity => Capacity + ExtraCapacity;

    public int Reserved { get; set; }
    public int Available => TotalCapacity - Reserved;
}