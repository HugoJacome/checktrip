namespace CheckTrip.Web.Features.Security.Models;

public class CurrentUserModel
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public List<string> Roles { get; set; } = [];

    public DateTime LoginUtc { get; set; } = DateTime.UtcNow;
}