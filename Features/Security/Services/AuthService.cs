using CheckTrip.Web.Data;
using CheckTrip.Web.Features.Security.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Features.Security.Services;

public class AuthService
{
    private const string SessionKey = "checktrip_user";

    private readonly AppDbContext _db;
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly PasswordHasher<object> _passwordHasher = new();

    public AuthService(
        AppDbContext db,
        ProtectedSessionStorage sessionStorage)
    {
        _db = db;
        _sessionStorage = sessionStorage;
    }

    public async Task<CurrentUserModel?> LoginAsync(LoginModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username) ||
            string.IsNullOrWhiteSpace(model.Password))
            return null;

        var hash = _passwordHasher.HashPassword(new object(), model.Password);

        var username = model.Username.Trim().ToLowerInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(x =>
                x.Username == username &&
                x.IsActive);

        if (user is null)
            return null;

        var result = _passwordHasher.VerifyHashedPassword(
            new object(),
            user.PasswordHash,
            model.Password);

        if (result == PasswordVerificationResult.Failed)
            return null;

        var roles = await _db.UserRoles
            .Where(x =>
                x.TenantId == user.TenantId &&
                x.UserId == user.Id)
            .Join(
                _db.Roles,
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => r.Name
            )
            .ToListAsync();

        var currentUser = new CurrentUserModel
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Username = user.Username,
            FullName = user.FullName,
            Roles = roles
        };

        await _sessionStorage.SetAsync(SessionKey, currentUser);

        return currentUser;
    }

    public async Task<CurrentUserModel?> GetCurrentUserAsync()
    {
        var result = await _sessionStorage.GetAsync<CurrentUserModel>(SessionKey);

        return result.Success ? result.Value : null;
    }

    public async Task LogoutAsync()
    {
        await _sessionStorage.DeleteAsync(SessionKey);
    }

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(new object(), password);
    }
}