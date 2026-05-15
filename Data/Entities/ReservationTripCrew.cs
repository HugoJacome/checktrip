namespace CheckTrip.Web.Data.Entities;

public class ReservationTripCrew : BaseEntity
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = default!;

    public Guid BoatId { get; set; }
    public Boat Boat { get; set; } = default!;

    public string TripType { get; set; } = default!; // Outbound / Return

    public string CaptainName { get; set; } = default!;
    public string? CaptainDocument { get; set; }

    public string? Sailor1Name { get; set; }
    public string? Sailor1Document { get; set; }

    public string? Sailor2Name { get; set; }
    public string? Sailor2Document { get; set; }

    public string? Sailor3Name { get; set; }
    public string? Sailor3Document { get; set; }

    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;
}