public class RouteListItem
{
    public Guid Id { get; set; }
    public string Origin { get; set; } = default!;
    public string Destination { get; set; } = default!;
    public string? Description { get; set; }
}