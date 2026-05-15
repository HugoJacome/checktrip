using CheckTrip.Web.Data.Entities;

public class Ticket : BaseEntity
{
    public Guid? ReservationItemId { get; set; }
    public ReservationItem? ReservationItem { get; set; }

    public Guid? BoatId { get; set; }
    public Boat? Boat { get; set; }

    public DateTime? TripDate { get; set; }

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