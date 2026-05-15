namespace CheckTrip.Web.Data.Entities;

public class TripRoute : BaseEntity
{
    public string Origin { get; set; } = default!;
    public string Destination { get; set; } = default!;

    public string Description { get; set; } = "default";
    public string? Place { get; set; }

    public TimeOnly? EstimatedTime { get; set; }

    public string? Type { get; set; }

    public bool IsActive { get; set; } = true;
}