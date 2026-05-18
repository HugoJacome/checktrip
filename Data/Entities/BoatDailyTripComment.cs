namespace CheckTrip.Web.Data.Entities;

public class BoatDailyTripComment : BaseEntity
{
    public Guid BoatDailyTripId { get; set; }
    public BoatDailyTrip BoatDailyTrip { get; set; } = default!;

    public string CommentType { get; set; } = "General";
    public string Comment { get; set; } = default!;

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;
}