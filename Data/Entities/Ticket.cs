namespace CheckTrip.Web.Data.Entities;

public class Ticket : BaseEntity
{
    public Guid ReservationItemId { get; set; }
    public ReservationItem ReservationItem { get; set; } = default!;

    public string TicketNumber { get; set; } = default!;
    public string? TicketType { get; set; }
    public string? Color { get; set; }

    public bool IsPrinted { get; set; }
    public DateTime? PrintedAt { get; set; }

    public Guid? PrintedByUserId { get; set; }
    public User? PrintedByUser { get; set; }

    public int ReprintCount { get; set; }
    public DateTime? LastReprintAt { get; set; }
}