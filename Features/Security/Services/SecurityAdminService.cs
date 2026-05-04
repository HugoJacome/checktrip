using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;
using CheckTrip.Web.Features.Security.Models;
using CheckTrip.Web.Infrastructure.Audit;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Features.Security.Services;

public class SecurityAdminService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IAuditService _audit;
    private readonly PasswordHasher<object> _passwordHasher = new();

    public SecurityAdminService(
        AppDbContext db,
        ITenantProvider tenant,
        IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<List<UserListItem>> GetUsersAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Username)
            .Select(x => new UserListItem
            {
                Id = x.Id,
                Username = x.Username,
                FullName = x.FullName,
                IsActive = x.IsActive,
                Roles = string.Join(", ", x.UserRoles.Select(r => r.Role.Name))
            })
            .ToListAsync();
    }

    public async Task<UserEditModel?> GetUserAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        var user = await _db.Users
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

        if (user is null)
            return null;

        return new UserEditModel
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            IsActive = user.IsActive,
            RoleIds = user.UserRoles.Select(x => x.RoleId).ToList()
        };
    }

    public async Task SaveUserAsync(UserEditModel model)
    {
        var tenantId = _tenant.GetTenantId();

        if (string.IsNullOrWhiteSpace(model.Username))
            throw new Exception("El usuario es obligatorio.");

        if (string.IsNullOrWhiteSpace(model.FullName))
            throw new Exception("El nombre completo es obligatorio.");

        var username = model.Username.Trim().ToLower();

        var exists = await _db.Users.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.Username.ToLower() == username &&
            x.Id != model.Id);

        if (exists)
            throw new Exception("Ya existe un usuario con ese login.");

        if (model.Id is null)
        {
            if (string.IsNullOrWhiteSpace(model.Password))
                throw new Exception("La contraseña es obligatoria para usuarios nuevos.");

            var user = new User
            {
                TenantId = tenantId,
                Username = username,
                FullName = model.FullName.Trim(),
                PasswordHash = _passwordHasher.HashPassword(new object(), model.Password),
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var roleId in model.RoleIds.Distinct())
            {
                user.UserRoles.Add(new UserRole
                {
                    TenantId = tenantId,
                    RoleId = roleId
                });
            }

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("User", "Create", null, new
            {
                user.Id,
                user.Username,
                user.FullName,
                model.RoleIds
            });
        }
        else
        {
            var user = await _db.Users
                .Include(x => x.UserRoles)
                .FirstOrDefaultAsync(x => x.Id == model.Id.Value && x.TenantId == tenantId);

            if (user is null)
                throw new Exception("Usuario no encontrado.");

            var old = new
            {
                user.Username,
                user.FullName,
                user.IsActive,
                RoleIds = user.UserRoles.Select(x => x.RoleId).ToList()
            };

            user.Username = username;
            user.FullName = model.FullName.Trim();
            user.IsActive = model.IsActive;

            if (!string.IsNullOrWhiteSpace(model.Password))
                user.PasswordHash = _passwordHasher.HashPassword(new object(), model.Password);

            _db.UserRoles.RemoveRange(user.UserRoles);

            foreach (var roleId in model.RoleIds.Distinct())
            {
                user.UserRoles.Add(new UserRole
                {
                    TenantId = tenantId,
                    UserId = user.Id,
                    RoleId = roleId
                });
            }

            await _db.SaveChangesAsync();

            await _audit.LogAsync("User", "Update", old, new
            {
                user.Id,
                user.Username,
                user.FullName,
                user.IsActive,
                model.RoleIds
            });
        }
    }

    public async Task<List<RoleListItem>> GetRolesAsync()
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Roles
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => new RoleListItem
            {
                Id = x.Id,
                Name = x.Name,
                IsActive = x.IsActive
            })
            .ToListAsync();
    }

    public async Task<RoleEditModel?> GetRoleAsync(Guid id)
    {
        var tenantId = _tenant.GetTenantId();

        return await _db.Roles
            .Where(x => x.Id == id && x.TenantId == tenantId)
            .Select(x => new RoleEditModel
            {
                Id = x.Id,
                Name = x.Name,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync();
    }

    public async Task SaveRoleAsync(RoleEditModel model)
    {
        var tenantId = _tenant.GetTenantId();

        if (string.IsNullOrWhiteSpace(model.Name))
            throw new Exception("El nombre del rol es obligatorio.");

        var name = model.Name.Trim();

        var exists = await _db.Roles.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.Name.ToLower() == name.ToLower() &&
            x.Id != model.Id);

        if (exists)
            throw new Exception("Ya existe un rol con ese nombre.");

        if (model.Id is null)
        {
            var role = new Role
            {
                TenantId = tenantId,
                Name = name,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _db.Roles.Add(role);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Role", "Create", null, role);
        }
        else
        {
            var role = await _db.Roles.FirstOrDefaultAsync(x =>
                x.Id == model.Id.Value &&
                x.TenantId == tenantId);

            if (role is null)
                throw new Exception("Rol no encontrado.");

            var old = new { role.Name, role.IsActive };

            role.Name = name;
            role.IsActive = model.IsActive;

            await _db.SaveChangesAsync();

            await _audit.LogAsync("Role", "Update", old, role);
        }
    }

    public async Task<List<PermissionEditModel>> GetPermissionsAsync(Guid roleId)
    {
        var tenantId = _tenant.GetTenantId();

        var resources = await _db.Resources
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        var permissions = await _db.RoleResourcePermissions
            .Where(x => x.TenantId == tenantId && x.RoleId == roleId)
            .ToListAsync();

        return resources.Select(resource =>
        {
            var permission = permissions.FirstOrDefault(x => x.ResourceId == resource.Id);

            return new PermissionEditModel
            {
                ResourceId = resource.Id,
                ResourceCode = resource.Code,
                ResourceName = resource.Name,
                CanView = permission?.CanView ?? false,
                CanCreate = permission?.CanCreate ?? false,
                CanEdit = permission?.CanEdit ?? false,
                CanDelete = permission?.CanDelete ?? false,
                CanManage = permission?.CanManage ?? false
            };
        }).ToList();
    }

    public async Task SavePermissionsAsync(Guid roleId, List<PermissionEditModel> permissions)
    {
        var tenantId = _tenant.GetTenantId();

        var roleExists = await _db.Roles.AnyAsync(x =>
            x.Id == roleId &&
            x.TenantId == tenantId);

        if (!roleExists)
            throw new Exception("Rol no encontrado.");

        var current = await _db.RoleResourcePermissions
            .Where(x => x.TenantId == tenantId && x.RoleId == roleId)
            .ToListAsync();

        _db.RoleResourcePermissions.RemoveRange(current);

        foreach (var p in permissions)
        {
            _db.RoleResourcePermissions.Add(new RoleResourcePermission
            {
                TenantId = tenantId,
                RoleId = roleId,
                ResourceId = p.ResourceId,
                CanView = p.CanView,
                CanCreate = p.CanCreate,
                CanEdit = p.CanEdit,
                CanDelete = p.CanDelete,
                CanManage = p.CanManage
            });
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync("RoleResourcePermission", "Update", null, new
        {
            RoleId = roleId,
            Permissions = permissions
        });
    }
}