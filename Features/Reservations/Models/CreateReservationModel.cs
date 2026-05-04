namespace CheckTrip.Web.Features.Reservations.Models;

public class CreateReservationModel
{
    public Guid? AgencyId { get; set; }
    public Guid? SellerId { get; set; }

    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ExternalReference { get; set; }

    public Guid? OutboundRouteScheduleId { get; set; }
    public DateTime? OutboundDate { get; set; }

    public Guid? ReturnRouteScheduleId { get; set; }
    public DateTime? ReturnDate { get; set; }

    public string PaymentStatus { get; set; } = "Pending";
    public string? Notes { get; set; }

    public List<ReservationPassengerModel> Passengers { get; set; } = [];
}