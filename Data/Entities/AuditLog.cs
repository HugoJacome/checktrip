public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? TenantId { get; set; }

    public string EntityName { get; set; } = default!;
    public string Action { get; set; } = default!;

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public Guid? UserId { get; set; }
    public string? Username { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}