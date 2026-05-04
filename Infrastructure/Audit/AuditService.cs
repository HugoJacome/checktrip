using System.Text.Json;
using CheckAccess.Infrastructure.Auth;
using CheckTrip.Web.Data;
using CheckTrip.Web.Data.Entities;

namespace CheckTrip.Web.Infrastructure.Audit;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _user;

    public AuditService(AppDbContext db,
        ITenantProvider tenant,
        ICurrentUserService user)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
    }

    public async Task LogAsync(string entity, string action, object? oldValue, object? newValue)
    {
        var log = new AuditLog
        {
            TenantId = _tenant.GetTenantId(),
            EntityName = entity,
            Action = action,
            OldValues = oldValue != null ? JsonSerializer.Serialize(oldValue) : null,
            NewValues = newValue != null ? JsonSerializer.Serialize(newValue) : null,
            UserId = _user.UserId,
            Username = _user.Username,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }
}