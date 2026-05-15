namespace CheckTrip.Web.Data.Entities;

public class Role : BaseEntity
{
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    public List<UserRole> UserRoles { get; set; } = [];
    public List<RoleResourcePermission> Permissions { get; set; } = [];
}