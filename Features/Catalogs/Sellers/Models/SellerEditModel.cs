public class SellerEditModel
{
    public Guid? Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public bool IsActive { get; set; } = true;
}