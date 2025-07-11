using Android.App;
using Android.OS;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;

namespace Manta.Services;

public class AppLifecycleService : Java.Lang.Object, Android.App.Application.IActivityLifecycleCallbacks
{
    private CancellationTokenSource _cancellationTokenSource = new();
    private readonly object _lock = new();

    public void OnActivityCreated(Activity activity, Bundle? savedInstanceState) { }

    public void OnActivityDestroyed(Activity activity) { }

    public void OnActivityPaused(Activity activity) { }

    public void OnActivityResumed(Activity activity) { }

    public void OnActivitySaveInstanceState(Activity activity, Bundle outState) { }

    public void OnActivityStarted(Activity activity)
    {
        lock (_lock)
        {
            Console.WriteLine("App RESUMED from background");

            AlarmUtils.CancelAlarm(Android.App.Application.Context);

            PauseTokenSource.Resume();

            try
            {
                var serviceProvider = IPlatformApplication.Current?.Services;
                if (serviceProvider is not null)
                {
                    var notificationSingleton = serviceProvider.GetRequiredService<NotificationSingleton>();
                    notificationSingleton.Start(_cancellationTokenSource.Token);
                }
            }
            catch
            {

            }
        }
    }

    public void OnActivityStopped(Activity activity)
    {
        Console.WriteLine("App WENT TO SLEEP");

        lock (_lock)
        {
            try
            {
                // Unregister and switch to polling with alarm
                var serviceProvider = IPlatformApplication.Current?.Services;
                if (serviceProvider is not null)
                {
                    var notificationSingleton = serviceProvider.GetRequiredService<NotificationSingleton>();
                    Task.Run(notificationSingleton.StopNotificationListenerAsync).GetAwaiter().GetResult();
                }
            }
            finally
            {

            }

            PauseTokenSource.Pause();

            if (SecureStorageHelper.Get<bool>("notifications-enabled") && (SecureStorageHelper.Get<DaemonInstallOptions>("daemon-installation-type") == DaemonInstallOptions.RemoteNode))
            {
                AlarmUtils.ScheduleExactAlarm(Android.App.Application.Context);
            }
        }
    }
}
