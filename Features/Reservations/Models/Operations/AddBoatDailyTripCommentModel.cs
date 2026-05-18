namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class AddBoatDailyTripCommentModel
{
    public Guid BoatDailyTripId { get; set; }

    public string CommentType { get; set; } = "General";
    public string Comment { get; set; } = default!;
}