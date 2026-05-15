using CheckTrip.Web.Data.Entities;

public class Seller : BaseEntity
{
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }

    public string FirstName { get; set; } = default!;
    public string? LastName { get; set; }

    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public DateOnly? StartDate { get; set; }

    public bool SellsTransport { get; set; } = true;
    public bool SellsTours { get; set; }
    public bool SellsServices { get; set; }
    public bool HasCommission { get; set; }
    public bool PaysReservation { get; set; }

    public bool IsActive { get; set; } = true;

    public List<SellerRouteCommission> RouteCommissions { get; set; } = new();
}