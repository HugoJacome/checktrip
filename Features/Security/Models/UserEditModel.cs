namespace CheckTrip.Web.Features.Security.Models;

public class UserEditModel
{
    public Guid? Id { get; set; }
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Password { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Guid> RoleIds { get; set; } = [];
}