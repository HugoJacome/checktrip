using CheckTrip.Web.Data.Entities;

public class SellerRouteCommission : BaseEntity
{
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; } = default!;

    public Guid RouteId { get; set; }
    public TripRoute Route { get; set; } = default!;

    public decimal Commission { get; set; }

    public bool IsActive { get; set; } = true;
}