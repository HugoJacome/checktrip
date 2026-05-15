namespace CheckTrip.Web.Data.Entities;

public class ReservationComment : BaseEntity
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = default!;

    public string CommentType { get; set; } = "General";
    public string Comment { get; set; } = default!;

    public string? PaymentStatus { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;
}