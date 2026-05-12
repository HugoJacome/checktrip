namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class ReservationPassengerOperationItem
{
    public Guid ReservationId { get; set; }
    public Guid ReservationItemId { get; set; }

    public string ReservationCode { get; set; } = default!;
    public string? ExternalReference { get; set; }

    public string? Agency { get; set; }
    public string? Seller { get; set; }

    public string DocumentType { get; set; } = default!;
    public string DocumentNumber { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Nationality { get; set; }
    public int? Age { get; set; }

    public string PassengerType { get; set; } = default!;
    public string TripType { get; set; } = default!;

    public string? OutboundBoat { get; set; }
    public string? OutboundRoute { get; set; }
    public string? OutboundSchedule { get; set; }
    public DateOnly? OutboundTravelDate { get; set; }

    public string? ReturnBoat { get; set; }
    public string? ReturnRoute { get; set; }
    public string? ReturnSchedule { get; set; }
    public DateOnly? ReturnTravelDate { get; set; }

    public string Status { get; set; } = default!;
    public string PaymentStatus { get; set; } = default!;
}