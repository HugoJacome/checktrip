namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class ChangeBoatDailyTripModel
{
    public Guid BoatDailyTripId { get; set; }

    public Guid NewBoatRouteScheduleId { get; set; }

    public string Reason { get; set; } = default!;
}