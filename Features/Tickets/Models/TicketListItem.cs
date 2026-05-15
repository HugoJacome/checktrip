namespace CheckTrip.Web.Features.Tickets.Models;

public class TicketListItem
{
    public Guid Id { get; set; }
    public Guid? ReservationItemId { get; set; }

    public string TicketNumber { get; set; } = default!;
    public string PassengerName { get; set; } = default!;
    public string DocumentNumber { get; set; } = default!;

    public string TripType { get; set; } = default!;
    public bool IsGeneric { get; set; }

    public bool IsPrinted { get; set; }
    public DateTime? PrintedAt { get; set; }
    public int ReprintCount { get; set; }
}   