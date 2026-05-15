namespace CheckTrip.Web.Data.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}