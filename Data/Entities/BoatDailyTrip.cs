using CheckTrip.Web.Data.Entities;

public class BoatDailyTrip : BaseEntity
{
    public Guid BoatId { get; set; }
    public Boat Boat { get; set; } = default!;

    public DateOnly TripDate { get; set; }

    public string Status { get; set; } = "Open";
    // Open, DocumentGenerated, Closed

    public DateTime? DocumentGeneratedAt { get; set; }
    public Guid? DocumentGeneratedByUserId { get; set; }

    public string? DocumentNumber { get; set; }
    public string? DocumentPath { get; set; }
    public DateTime UpdatedAt { get; internal set; }
}