using CheckTrip.Web.Data.Entities;

public class Ticket : BaseEntity
{
    public Guid? ReservationPassengerTripId { get; set; }
    public ReservationPassengerTrip? ReservationPassengerTrip { get; set; }

    public Guid? BoatDailyTripId { get; set; }
    public BoatDailyTrip? BoatDailyTrip { get; set; }

    public Guid? BoatId { get; set; }
    public Boat? Boat { get; set; }

    public DateOnly? TripDate { get; set; }

    public string TicketNumber { get; set; } = default!;
    public string? TicketType { get; set; }
    public string? Color { get; set; }

    public string? GenericPassengerName { get; set; }
    public string? GenericDocumentNumber { get; set; }

    public bool IsPrinted { get; set; }
    public DateTime? PrintedAt { get; set; }
    public Guid? PrintedByUserId { get; set; }

    public int ReprintCount { get; set; }
    public DateTime? LastReprintAt { get; set; }
}