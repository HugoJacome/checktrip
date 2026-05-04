public class SellerListItem
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }

    public string Name => $"{FirstName} {LastName}".Trim();

    public bool IsActive { get; set; }
}