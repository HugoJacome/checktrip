namespace CheckTrip.Web.Features.Security.Models;

public class UserListItem
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public bool IsActive { get; set; }
    public string Roles { get; set; } = "";
}