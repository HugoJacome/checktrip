namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class FinishReservationModel
{
    public Guid ReservationId { get; set; }

    public bool ApplyToOutbound { get; set; } = true;
    public bool ApplyToReturn { get; set; } = true;

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