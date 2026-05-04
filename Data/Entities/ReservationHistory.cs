using CheckTrip.Web.Data.Entities;

public class ReservationHistory : BaseEntity
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = default!;

    public string Action { get; set; } = default!;
    public string? Reason { get; set; }

    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }

    public Guid CreatedByUserId { get; set; }
}