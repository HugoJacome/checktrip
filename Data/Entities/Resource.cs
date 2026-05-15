namespace CheckTrip.Web.Data.Entities;

public class Resource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? MenuPath { get; set; }
    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public List<RoleResourcePermission> Permissions { get; set; } = [];
}