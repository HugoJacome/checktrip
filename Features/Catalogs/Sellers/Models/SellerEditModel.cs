using CheckAccess.Features.Catalogs.Agencies.Models;

public class SellerEditModel
{
    public Guid? Id { get; set; }

    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }

    public string FirstName { get; set; } = string.Empty;
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

    public List<RouteValueItem> Routes { get; set; } = new();
}