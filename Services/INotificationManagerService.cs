namespace Manta.Services;

public interface INotificationManagerService
{
    event EventHandler NotificationReceived;
    void SendNotification(string title, string message, string? urlToOpen = null, DateTime? notifyTime = null);
    void ReceiveNotification(string title, string message);
}