namespace CheckTrip.Web.Features.Reservations.Models;

public class ReservationMonitorItem
{
    public Guid Id { get; set; }
    public string ReservationCode { get; set; } = default!;

    public string? Agency { get; set; }
    public string? Seller { get; set; }

    public string Status { get; set; } = default!;
    public string PaymentStatus { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
    public int PassengerCount { get; set; }

    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
}