namespace CheckTrip.Web.Features.Reservations.Models;

public class ReservationDetailModel
{
    public Guid Id { get; set; }
    public string ReservationCode { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string PaymentStatus { get; set; } = default!;
    public string? Agency { get; set; }
    public string? Seller { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<ReservationPassengerDetailModel> Passengers { get; set; } = [];
}