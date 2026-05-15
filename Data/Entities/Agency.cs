using CheckTrip.Web.Data.Entities;

public class Agency : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Ruc { get; set; }

    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }

    public string? Type { get; set; }

    public bool IsActive { get; set; } = true;

    public List<AgencyRouteRate> RouteRates { get; set; } = new();
}