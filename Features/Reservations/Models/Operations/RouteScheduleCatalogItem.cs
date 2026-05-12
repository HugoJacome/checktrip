namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class RouteScheduleCatalogItem
{
    public Guid Id { get; set; }
    public Guid BoatId { get; set; }
    public Guid RouteId { get; set; }

    public string Boat { get; set; } = default!;
    public string Route { get; set; } = default!;
    public string Schedule { get; set; } = default!;

    public string DisplayName => $"{Boat} / {Route} / {Schedule}";
}