namespace CheckTrip.Web.Data.Entities;

public class BoatDailyTripCrew : BaseEntity
{
    public Guid BoatDailyTripId { get; set; }
    public BoatDailyTrip BoatDailyTrip { get; set; } = default!;

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