namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class ChangeReservationBoatModel
{
    public Guid ReservationId { get; set; }

    public Guid NewBoatRouteScheduleId { get; set; }

    public bool ApplyToOutbound { get; set; } = true;
    public bool ApplyToReturn { get; set; } = true;

    public string Reason { get; set; } = default!;
}