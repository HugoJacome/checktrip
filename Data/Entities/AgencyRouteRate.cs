using CheckTrip.Web.Data.Entities;

public class AgencyRouteRate : BaseEntity
{
    public Guid AgencyId { get; set; }
    public Agency Agency { get; set; } = default!;

    public Guid RouteId { get; set; }
    public TripRoute Route { get; set; } = default!;

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
}