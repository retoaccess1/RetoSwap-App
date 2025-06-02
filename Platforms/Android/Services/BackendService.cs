using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using HavenoSharp.Singletons;
using Manta.Helpers;
using System.Text;

namespace Manta.Services;

[Service(Name = "com.companyname.manta.BackendService", Enabled = true, Exported = true, Permission = "android.permission.BIND_JOB_SERVICE", ForegroundServiceType = Android.Content.PM.ForegroundService.TypeSystemExempted)]
[IntentFilter(["com.companyname.manta.ACTION_START_BACKEND"])]
public class BackendService : Service
{
    private string _notificationChannelId = "BackendServiceChannel";
    private NotificationCompat.Builder? _notificationBuilder;
    private NotificationManager? _notificationManager;

    private CancellationTokenSource? _torCts;
    private CancellationTokenSource? _daemonCts;
    private TaskCompletionSource? _torReadyTCS;
    private PowerManager.WakeLock? _wakeLock;

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
                .SetSmallIcon(Resource.Drawable.haveno)
                .SetContentIntent(pendingIntent)
                .SetCategory(Notification.CategoryService);
        }

        _notificationBuilder.SetOngoing(true);
        _notificationBuilder.SetContentTitle("test");
        _notificationBuilder.MActions?.Clear();

        _notificationBuilder.SetContentText("test");

        StartForeground(1, _notificationBuilder.Build());
    }

    public override void OnCreate()
    {
        base.OnCreate();

        if (_notificationManager is null)
        {
            _notificationManager = (NotificationManager?)GetSystemService(Context.NotificationService);
        }

        if (AndroidPermissionService.GetIgnoreBatteryOptimizationsEnabled())
            CreateNotificationChannel();
    }

    [return: GeneratedEnum]
    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        if (intent is null)
            return StartCommandResult.RedeliverIntent;

        var serviceProvider = IPlatformApplication.Current?.Services;
        var grpcChannelSingleton = serviceProvider?.GetService<GrpcChannelSingleton>();
        if (grpcChannelSingleton is null)
            throw new Exception("GrpcChannelSingleton was null in StartBackendServices");

        var host = intent.GetStringExtra("host");
        var password = intent.GetStringExtra("password");

        if (password is null || host == null)
            return StartCommandResult.RedeliverIntent;

        SecureStorageHelper.Set("password", password);
        SecureStorageHelper.Set("host", host);
        grpcChannelSingleton.CreateChannel(host, password);

        var torThread = new Thread(() =>
        {
            Proot.RunProotUbuntuCommand("tor");
        });
        torThread.Start();

        var daemonThread = new Thread(() =>
        {
            Proot.RunProotUbuntuCommand("bash", "-c", $"/usr/share/haveno/haveno-daemon --baseCurrencyNetwork=XMR_STAGENET --useLocalhostForP2P=false --useDevPrivilegeKeys=false --nodePort=9999 --appName=haveno-XMR_STAGENET_user1 --apiPassword={password} --apiPort=3201 --passwordRequired=false --useNativeXmrWallet=false --torControlHost=127.0.0.1 --torControlPort=9051");
        });
        daemonThread.Start();

        return StartCommandResult.RedeliverIntent;
    }

    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }

    //private async Task StartBackendServices(string password, string host)
    //{
    //    //var serviceProvider = MauiApplication.Current.Services;
    //    //var grpcChannelSingleton = serviceProvider.GetService<GrpcChannelSingleton>();
    //    //if (grpcChannelSingleton is null)
    //    //    throw new Exception("GrpcChannelSingleton was null in StartBackendServices");

    //    //SecureStorageHelper.Set("password", password);
    //    //SecureStorageHelper.Set("host", host);
    //    //grpcChannelSingleton.CreateChannel(host, password);

    //    //var torThread = new Thread(() =>
    //    //{
    //    //    Proot.RunProotUbuntuCommand("tor");
    //    //});
    //    //torThread.Start();

    //    //var daemonThread = new Thread(() =>
    //    //{
    //    //    Proot.RunProotUbuntuCommand("bash", "-c", $"/usr/share/haveno/haveno-daemon --baseCurrencyNetwork=XMR_STAGENET --useLocalhostForP2P=false --useDevPrivilegeKeys=false --nodePort=9999 --appName=haveno-XMR_STAGENET_user1 --apiPassword={password} --apiPort=3201 --passwordRequired=false --useNativeXmrWallet=false --torControlHost=127.0.0.1 --torControlPort=9051");
    //    //});
    //    //daemonThread.Start();
    //}

    private async void A(string password, string host, Action<string>? progressCb = default)
    {
        var serviceProvider = IPlatformApplication.Current?.Services;
        var grpcChannelSingleton = serviceProvider?.GetService<GrpcChannelSingleton>();
        if (grpcChannelSingleton is null)
            throw new Exception("GrpcChannelSingleton was null in StartBackendServices");

        await SecureStorageHelper.SetAsync("password", password);
        await SecureStorageHelper.SetAsync("host", host);
        grpcChannelSingleton.CreateChannel(host, password);

        // TODO
        // When app uses optimized battery settings, these threads get killed.
        // I want these to just sleep when optimized so they resume when the app resumes

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

                            progressCb?.Invoke($"Tor bootstrapping: {percentage}%");

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

            progressCb?.Invoke("Starting daemon");

            _daemonCts = new();
            using var streamReader = Proot.RunProotUbuntuCommand("bash", _daemonCts.Token, "-c", $"/usr/share/haveno/haveno-daemon --baseCurrencyNetwork=XMR_STAGENET --useLocalhostForP2P=false --useDevPrivilegeKeys=false --nodePort=9999 --appName=haveno-XMR_STAGENET_user1 --apiPassword={password} --apiPort=3201 --passwordRequired=false --useNativeXmrWallet=false --torControlHost=127.0.0.1 --torControlPort=9051");

            string? line;
            while ((line = streamReader.ReadLine()) != null)
            {
                Console.WriteLine(line);

                switch (line)
                {
                    case "Init wallet":
                        progressCb?.Invoke("Initializing wallet");
                        break;
                    default: break;
                }
            }
        });
    }
}
