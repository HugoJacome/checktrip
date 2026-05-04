using CheckTrip.Web.Data.Entities;

public class Schedule : BaseEntity
{
    public string Name { get; set; } = default!;
    public TimeSpan DepartureTime { get; set; }
    public bool IsActive { get; set; } = true;
}