namespace Manta.Services;

public class WindowsNotificationManagerService : INotificationManagerService
{
    public WindowsNotificationManagerService()
    {

    }

    public event EventHandler? NotificationReceived;

    public void ReceiveNotification(string title, string message)
    {
        return;
    }

    public void SendNotification(string title, string message, string? urlToOpen = null, DateTime? notifyTime = null)
    {
        return;
    }
}
