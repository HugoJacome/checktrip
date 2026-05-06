using CheckTrip.Web.Data.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = default!;
    public string NormalizedUsername { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime? LastFailedLoginUtc { get; set; }

    public List<UserRole> UserRoles { get; set; } = new();
}