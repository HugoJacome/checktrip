using CheckTrip.Web.Data.Entities;

public class Seller : BaseEntity
{
    public string FirstName { get; set; } = default!;
    public string? LastName { get; set; }
    public bool IsActive { get; set; } = true;
}