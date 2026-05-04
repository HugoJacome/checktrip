public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Subdomain { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}