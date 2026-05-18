namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class FinishBoatDailyTripModel
{
    public Guid BoatDailyTripId { get; set; }

    public string CaptainName { get; set; } = default!;
    public string? CaptainDocument { get; set; }

    public string? Sailor1Name { get; set; }
    public string? Sailor1Document { get; set; }

    public string? Sailor2Name { get; set; }
    public string? Sailor2Document { get; set; }

    public string? Sailor3Name { get; set; }
    public string? Sailor3Document { get; set; }

    public string? Notes { get; set; }
}