namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class AddReservationCommentModel
{
    public Guid ReservationId { get; set; }

    public string CommentType { get; set; } = "General";
    public string Comment { get; set; } = default!;

    public string? PaymentStatus { get; set; }
}