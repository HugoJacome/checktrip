namespace CheckTrip.Web.Features.Tickets.Models;

public class TicketGenerateResult
{
    public Guid ReservationPassengerTripId { get; set; }
    public Guid? TicketId { get; set; }

    public bool Success { get; set; }
    public string? TicketNumber { get; set; }
    public string? Error { get; set; }
}