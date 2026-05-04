using CheckTrip.Web.Data.Entities;

public class Route : BaseEntity
{
    public string Origin { get; set; } = default!;
    public string Destination { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}