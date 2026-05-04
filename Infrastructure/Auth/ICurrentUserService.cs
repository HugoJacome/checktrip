using CheckTrip.Web.Features.Security.Models;

namespace CheckAccess.Infrastructure.Auth;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    Guid? TenantId { get; }

    Task<CurrentUserModel?> LoadAsync();
}