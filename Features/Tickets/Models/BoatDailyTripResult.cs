namespace CheckTrip.Web.Features.Tickets.Models;

public class BoatDailyTripResult
{
    public Guid? Id { get; set; }
    public Guid BoatId { get; set; }
    public DateTime TripDate { get; set; }

    public string Status { get; set; } = "Open";
    public bool IsLocked => Status is "DocumentGenerated" or "Closed";

    public DateTime? DocumentGeneratedAt { get; set; }
    public string? DocumentNumber { get; set; }
    public string? DocumentPath { get; set; }
}