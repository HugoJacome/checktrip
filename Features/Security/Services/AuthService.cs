using CheckTrip.Web.Data;
using CheckTrip.Web.Features.Security.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Features.Security.Services;

public class AuthService
{
    private const string SessionKey = "checktrip_user";

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(10);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly PasswordHasher<object> _passwordHasher = new();

    public AuthService(
        IDbContextFactory<AppDbContext> dbFactory,
        ProtectedSessionStorage sessionStorage)
    {
        _dbFactory = dbFactory;
        _sessionStorage = sessionStorage;
    }

    public async Task<CurrentUserModel?> LoginAsync(LoginModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username) ||
            string.IsNullOrWhiteSpace(model.Password))
            return null;

        var username = NormalizeUsername(model.Username);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var user = await db.Users
            .FirstOrDefaultAsync(x =>
                x.NormalizedUsername == username &&
                x.IsActive);

        if (user is null)
            return null;

        if (IsLockedOut(user))
            return null;

        var result = _passwordHasher.VerifyHashedPassword(
            new object(),
            user.PasswordHash,
            model.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            await RegisterFailedLoginAsync(db, user);
            return null;
        }

        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginUtc = DateTime.UtcNow;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = HashPassword(model.Password);

        await db.SaveChangesAsync();

        var roles = await db.UserRoles
            .AsNoTracking()
            .Where(x =>
                x.TenantId == user.TenantId &&
                x.UserId == user.Id)
            .Join(
                db.Roles.AsNoTracking(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => r.Name
            )
            .Distinct()
            .ToListAsync();

        var currentUser = new CurrentUserModel
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Username = user.Username,
            FullName = user.FullName,
            Roles = roles,
            LoginUtc = DateTime.UtcNow
        };

        await _sessionStorage.SetAsync(SessionKey, currentUser);

        return currentUser;
    }

    public async Task<CurrentUserModel?> GetCurrentUserAsync()
    {
        var result = await _sessionStorage.GetAsync<CurrentUserModel>(SessionKey);

        if (!result.Success || result.Value is null)
            return null;

        return result.Value;
    }

    public async Task LogoutAsync()
    {
        await _sessionStorage.DeleteAsync(SessionKey);
    }

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(new object(), password);
    }

    public string NormalizeUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }

    private static bool IsLockedOut(User user)
    {
        return user.LockoutEndUtc.HasValue &&
               user.LockoutEndUtc.Value > DateTime.UtcNow;
    }

    private static async Task RegisterFailedLoginAsync(AppDbContext db, User user)
    {
        user.AccessFailedCount += 1;
        user.LastFailedLoginUtc = DateTime.UtcNow;

        if (user.AccessFailedCount >= MaxFailedAttempts)
            user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutTime);

        await db.SaveChangesAsync();
    }
}