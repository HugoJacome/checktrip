namespace CheckTrip.Web.Features.Reservations.Models.Operations;

public class BoatDailyTripCatalogItem
{
    public Guid Id { get; set; }

    public DateOnly TripDate { get; set; }

    public Guid BoatRouteScheduleId { get; set; }
    public Guid BoatId { get; set; }
    public Guid RouteId { get; set; }
    public Guid ScheduleId { get; set; }

    public string Boat { get; set; } = default!;
    public string Route { get; set; } = default!;
    public string Schedule { get; set; } = default!;
    public string Status { get; set; } = default!;

    public string DisplayName => $"{TripDate:yyyy-MM-dd} / {Boat} / {Route} / {Schedule}";
}