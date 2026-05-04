using CheckAccess.Infrastructure.Auth;
using CheckTrip.Web.Features.Security.Models;
using CheckTrip.Web.Features.Security.Services;

namespace CheckTrip.Web.Infrastructure.Auth;

public class CurrentUserService : ICurrentUserService
{
    private readonly AuthService _authService;

    public CurrentUserService(AuthService authService)
    {
        _authService = authService;
    }

    public Guid? UserId { get; private set; }
    public string? Username { get; private set; }
    public Guid? TenantId { get; private set; }

    public async Task<CurrentUserModel?> LoadAsync()
    {
        var user = await _authService.GetCurrentUserAsync();

        UserId = user?.UserId;
        Username = user?.Username;
        TenantId = user?.TenantId;

        return user;
    }
}