using CheckAccess.Features.Catalogs.Agencies.Models;

public class AgencyEditModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Ruc { get; set; }

    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }

    public string? Type { get; set; }

    public bool IsActive { get; set; } = true;

    public List<RouteValueItem> Routes { get; set; } = new();
}