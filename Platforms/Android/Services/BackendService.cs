using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using System.Text;

namespace Manta.Services;

[Service(Name = "com.companyname.manta.BackendService", Enabled = true, Exported = false, Permission = "android.permission.BIND_VPN_SERVICE", ForegroundServiceType = Android.Content.PM.ForegroundService.TypeDataSync)]
[IntentFilter(["com.companyname.manta.ACTION_START_BACKEND", "com.companyname.manta.ACTION_STOP_BACKEND"])]
public class BackendService : Service
{
    private string _notificationChannelId = "BackendServiceChannel";
    private NotificationCompat.Builder? _notificationBuilder;
    private NotificationManager? _notificationManager;

    private CancellationTokenSource? _torCts;
    private CancellationTokenSource? _daemonCts;
    private TaskCompletionSource? _torReadyTCS;

    public BackendService()
    {
        
    }

#pragma warning disable CA1416
    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var channel = new NotificationChannel(_notificationChannelId, "Haveno", NotificationImportance.Low)
        {
            Description = "Channel for Haveno backend",
            LockscreenVisibility = NotificationVisibility.Secret
        };

        channel.EnableLights(false);
        channel.EnableVibration(false);
        channel.SetShowBadge(false);

        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.CreateNotificationChannel(channel);
    }
#pragma warning restore

    private void ShowNotification()
    {
        if (string.IsNullOrEmpty(PackageName) || PackageManager is null)
            return;

        var intent = PackageManager.GetLaunchIntentForPackage(PackageName);
        var pendingIntent = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable);

        if (_notificationBuilder is null)
        {
            _notificationManager = (NotificationManager?)GetSystemService(Context.NotificationService);
            _notificationBuilder = new NotificationCompat.Builder(this, _notificationChannelId)
                .SetSmallIcon(Resource.Drawable.small_icon)
                .SetContentIntent(pendingIntent)
                .SetPriority(NotificationCompat.PriorityMin)
                .SetVisibility(NotificationCompat.VisibilitySecret)
                .SetCategory(Notification.CategoryService)
                .SetShowWhen(false)
                .SetSilent(true)
                .SetOngoing(true);
        }

        _notificationBuilder.MActions?.Clear();

        StartForeground(1, _notificationBuilder.Build());
    }

    public override void OnCreate()
    {
        base.OnCreate();

        if (_notificationManager is null)
        {
            _notificationManager = (NotificationManager?)GetSystemService(Context.NotificationService);
        }

        CreateNotificationChannel();
    }

    [return: GeneratedEnum]
    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        if (intent is null)
            return StartCommandResult.RedeliverIntent;

        ShowNotification();

        switch (intent.Action) 
        {
            case "ACTION_STOP_BACKEND":
                Task.Run(StopHavenoDaemonAsync);
                break;
            case "ACTION_START_BACKEND":
                {
                    // TODO If running cancel
                    var password = intent.GetStringExtra("password");

                    if (password is null)
                        return StartCommandResult.RedeliverIntent;

                    Task.Run(() => StartBackend(password));
                }
                break;
            default: break;
        }

        return StartCommandResult.RedeliverIntent;
    }

    void UpdateProgress(string progress, bool isDone = false)
    {
        var intent = new Intent("com.companyname.manta.BACKEND_PROGRESS");
        intent.PutExtra("progress", progress);
        if (isDone)
            intent.PutExtra("isDone", isDone);

        intent.SetPackage(Android.App.Application.Context.PackageName);
        SendBroadcast(intent);
    }

    private void StartBackend(string password)
    {
        _torReadyTCS = new();

        _ = Task.Run(() =>
        {
            _torCts = new();
            using var streamReader = Proot.RunProotUbuntuCommand("tor", _torCts.Token);

            string? line;
            while ((line = streamReader.ReadLine()) != null)
            {
                Console.WriteLine(line);

                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] == '%')
                    {
                        StringBuilder stringBuilder = new();
                        for (int j = i - 1; j > 0; j--)
                        {
                            if (line[j] > '9' || line[j] < '0')
                                break;

                            stringBuilder.Append(line[j]);
                        }

                        if (stringBuilder.Length > 0)
                        {
                            var percentage = new string(stringBuilder.ToString().Reverse().ToArray());

                            UpdateProgress($"Tor bootstrapping: {percentage}%");

                            if (percentage == "100")
                            {
                                _torReadyTCS.SetResult();
                            }
                        }
                    }
                }
            }
        });

        _ = Task.Run(async () =>
        {
            await _torReadyTCS.Task;

            UpdateProgress("Starting daemon");

            _daemonCts = new();
            // Don't need to set a new password whenever starting daemon
            using var streamReader = Proot.RunProotUbuntuCommand("bash", _daemonCts.Token, "-c", $"/usr/share/haveno/haveno-daemon --baseCurrencyNetwork=XMR_STAGENET --useLocalhostForP2P=false --useDevPrivilegeKeys=false --nodePort=9999 --appName=haveno-XMR_STAGENET_user1 --apiPassword={password} --apiPort=3201 --passwordRequired=false --useNativeXmrWallet=false --torControlHost=127.0.0.1 --torControlPort=9051");

            string? line;
            while ((line = streamReader.ReadLine()) != null)
            {
                Console.WriteLine(line);

                if (line.Contains("Init wallet"))
                {
                    UpdateProgress("Initializing wallet", true);

                }
            }
        });
    }

    public async Task StopHavenoDaemonAsync()
    {
        if (_daemonCts is not null)
            await _daemonCts.CancelAsync();

        if (_torCts is not null)
            await _torCts.CancelAsync();
    }

    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }
}
