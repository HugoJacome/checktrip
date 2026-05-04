public class Resource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? MenuPath { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }

    public List<RoleResourcePermission> Permissions { get; set; } = [];
}