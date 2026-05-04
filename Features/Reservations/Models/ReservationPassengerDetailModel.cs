namespace CheckTrip.Web.Features.Reservations.Models;

public class ReservationPassengerDetailModel
{
    public string DocumentNumber { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public int? Age { get; set; }

    public string TripType { get; set; } = default!;
    public string PassengerType { get; set; } = default!;
    public string Status { get; set; } = default!;

    public string? OutboundTrip { get; set; }
    public DateOnly? OutboundDate { get; set; }

    public string? ReturnTrip { get; set; }
    public DateOnly? ReturnDate { get; set; }
}