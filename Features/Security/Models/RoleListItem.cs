namespace CheckTrip.Web.Features.Security.Models;

public class RoleListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; }
}