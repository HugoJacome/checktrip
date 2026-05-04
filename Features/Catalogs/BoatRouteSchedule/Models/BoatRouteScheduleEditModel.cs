public class BoatRouteScheduleEditModel
{
    public Guid? Id { get; set; }

    public Guid BoatId { get; set; }
    public Guid RouteId { get; set; }
    public Guid ScheduleId { get; set; }

    public decimal Price { get; set; }
    public string? Color { get; set; }
    public string? Days { get; set; }
}