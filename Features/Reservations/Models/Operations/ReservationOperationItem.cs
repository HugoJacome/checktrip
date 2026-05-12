namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class ReservationOperationItem
{
    public Guid Id { get; set; }

    public string ReservationCode { get; set; } = default!;
    public string? ExternalReference { get; set; }

    public string? Agency { get; set; }
    public string? Seller { get; set; }

    public string? Boat { get; set; }
    public string? Route { get; set; }
    public string? Schedule { get; set; }

    public DateOnly? TravelDate { get; set; }

    public string Status { get; set; } = default!;
    public string PaymentStatus { get; set; } = default!;

    public int PassengerCount { get; set; }
    public decimal TotalAmount { get; set; }

    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }

    public string? Notes { get; set; }
    public string? LastComment { get; set; }

    public DateTime CreatedAt { get; set; }
}