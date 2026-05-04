using CheckTrip.Web.Data.Entities;

public class ReservationItem : BaseEntity
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = default!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public string TripType { get; set; } = default!;
    public string PassengerType { get; set; } = "Normal";

    public Guid? OutboundRouteScheduleId { get; set; }
    public BoatRouteSchedule? OutboundRouteSchedule { get; set; }
    public DateOnly? OutboundTravelDate { get; set; }
    public int? OutboundSeatNumber { get; set; }

    public Guid? ReturnRouteScheduleId { get; set; }
    public BoatRouteSchedule? ReturnRouteSchedule { get; set; }
    public DateOnly? ReturnTravelDate { get; set; }
    public int? ReturnSeatNumber { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalPrice { get; set; }

    public string Status { get; set; } = "Reserved";
}