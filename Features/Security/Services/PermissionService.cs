using CheckAccess.Infrastructure.Auth;
using CheckTrip.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Features.Security.Services;

public class PermissionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public PermissionService(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<bool> CanViewAsync(string resourceCode)
    {
        return await HasPermissionAsync(resourceCode, "view");
    }

    public async Task<bool> CanCreateAsync(string resourceCode)
    {
        return await HasPermissionAsync(resourceCode, "create");
    }

    public async Task<bool> CanEditAsync(string resourceCode)
    {
        return await HasPermissionAsync(resourceCode, "edit");
    }

    public async Task<bool> CanDeleteAsync(string resourceCode)
    {
        return await HasPermissionAsync(resourceCode, "delete");
    }

    public async Task<bool> CanManageAsync(string resourceCode)
    {
        return await HasPermissionAsync(resourceCode, "manage");
    }

    private async Task<bool> HasPermissionAsync(string resourceCode, string permission)
    {
        var user = await _currentUser.LoadAsync();

        if (user is null)
            return false;

        if (user.Roles.Any(x => x.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
            return true;

        var query = _db.UserRoles
            .Where(ur =>
                ur.TenantId == user.TenantId &&
                ur.UserId == user.UserId)
            .SelectMany(ur => ur.Role.Permissions)
            .Where(p =>
                p.TenantId == user.TenantId &&
                p.Resource.Code == resourceCode &&
                p.Resource.IsActive);

        return permission switch
        {
            "view" => await query.AnyAsync(x => x.CanView || x.CanManage),
            "create" => await query.AnyAsync(x => x.CanCreate || x.CanManage),
            "edit" => await query.AnyAsync(x => x.CanEdit || x.CanManage),
            "delete" => await query.AnyAsync(x => x.CanDelete || x.CanManage),
            "manage" => await query.AnyAsync(x => x.CanManage),
            _ => false
        };
    }
}