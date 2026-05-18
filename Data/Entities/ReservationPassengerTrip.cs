using CheckTrip.Web.Data.Entities;

public class ReservationPassengerTrip : BaseEntity
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = default!;

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string? GenericPassengerName { get; set; }
    public string? GenericDocumentNumber { get; set; }

    public Guid BoatRouteScheduleId { get; set; }
    public BoatRouteSchedule BoatRouteSchedule { get; set; } = default!;

    public DateOnly TravelDate { get; set; }

    public string SegmentType { get; set; } = default!;
    public string PassengerType { get; set; } = "Adult";

    public int? SeatNumber { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalPrice { get; set; }

    public string Status { get; set; } = "Reserved";
}