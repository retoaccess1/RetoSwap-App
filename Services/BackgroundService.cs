#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using Manta.Singletons;

namespace Manta.Services;

// This uses too much battery and gets killed after 6hrs...
[Service(Exported = true, ForegroundServiceType = ForegroundService.TypeDataSync)]
public class BackgroundService : Service
{
    private readonly NotificationSingleton _notificationSingleton;
    private bool _isRunning;

    public BackgroundService()
    {
        var serviceProvider = IPlatformApplication.Current?.Services;
        if (serviceProvider is null)
            throw new Exception("serviceProvider was null");

        _notificationSingleton = serviceProvider.GetRequiredService<NotificationSingleton>();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (!_isRunning)
        {
            _isRunning = true;
            StartForeground(1, BuildNotification());
            Task.Run(_notificationSingleton.FetchInitial);
        }

        return StartCommandResult.Sticky;
    }

    // Needed since normally apps use firebase etc to receive updates and send notifs
    // In order to get updates from daemon we need this, at least for android 14+
    private Notification BuildNotification()
    {
        const string channelId = "daemon_service_channel";
        var channel = new NotificationChannel(channelId, "gRPC Service", NotificationImportance.Default)
        {
            Description = "Handles background daemon communication"
        };

        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.CreateNotificationChannel(channel);

        var builder = new NotificationCompat.Builder(this, channelId)
            //.SetContentTitle("Haveno daemon")
            //.SetContentText("")
            .SetSmallIcon(Resource.Drawable.haveno)
            .SetOngoing(true);

        return builder.Build();
    }

    public override void OnCreate()
    {
        base.OnCreate();
    }

    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }
}

#endif
