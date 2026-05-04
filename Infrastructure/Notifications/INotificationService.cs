namespace CheckTrip.Web.Infrastructure.Notifications;

public interface INotificationService
{
    void Success(string message);
    void Error(string message);
    void Warning(string message);
    void Info(string message);
}