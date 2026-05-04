public class BoatRouteScheduleListItem
{
    public Guid Id { get; set; }

    public string Boat { get; set; } = default!;
    public string Route { get; set; } = default!;
    public string Schedule { get; set; } = default!;

    public decimal Price { get; set; }
    public string? Color { get; set; }
    public string? Days { get; set; }
}