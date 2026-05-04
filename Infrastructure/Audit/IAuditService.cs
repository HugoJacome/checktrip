public interface IAuditService
{
    Task LogAsync(string entity, string action, object? oldValue, object? newValue);
}