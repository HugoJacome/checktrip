using CheckTrip.Web.Data.Entities;

public class Role : BaseEntity
{
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; }
    public List<UserRole> UserRoles { get; set; } = new();
    public List<RoleResourcePermission> Permissions { get; set; } = new();
}