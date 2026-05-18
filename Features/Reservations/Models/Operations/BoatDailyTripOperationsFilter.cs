namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class BoatDailyTripOperationsFilter
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public Guid? BoatId { get; set; }
    public Guid? RouteId { get; set; }
    public Guid? ScheduleId { get; set; }

    public Guid? AgencyId { get; set; }
    public Guid? SellerId { get; set; }

    public string? Status { get; set; }

    public bool? DocumentGenerated { get; set; }
}