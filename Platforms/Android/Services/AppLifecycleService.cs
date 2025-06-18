using Android.App;
using Android.OS;
using Manta.Helpers;
using Manta.Models;

namespace Manta.Services;

public class AppLifecycleService : Java.Lang.Object, Android.App.Application.IActivityLifecycleCallbacks
{
    public void OnActivityCreated(Activity activity, Bundle? savedInstanceState) { }

    public void OnActivityDestroyed(Activity activity) { }

    public void OnActivityPaused(Activity activity) { }

    public void OnActivityResumed(Activity activity) 
    {
        //var serviceProvider = IPlatformApplication.Current?.Services;
        //var grpcChannelSingleton = serviceProvider?.GetService<GrpcChannelSingleton>();

        //if (grpcChannelSingleton is not null)
        //{
        //    var host = SecureStorageHelper.Get<string>("host");
        //    var password = SecureStorageHelper.Get<string>("password");

        //    if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(password))
        //    {
        //        grpcChannelSingleton.CreateChannel(host, password);
        //    }
        //}
    }

    public void OnActivitySaveInstanceState(Activity activity, Bundle outState) { }

    public void OnActivityStarted(Activity activity)
    {
        Console.WriteLine("App RESUMED from background");

        AlarmUtils.CancelAlarm(Android.App.Application.Context);

        //var serviceProvider = IPlatformApplication.Current?.Services;
        //var grpcChannelSingleton = serviceProvider?.GetService<GrpcChannelSingleton>();

        //if (grpcChannelSingleton is not null)
        //{
        //    var host = SecureStorageHelper.Get<string>("host");
        //    var password = SecureStorageHelper.Get<string>("password");

        //    if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(password))
        //    {
        //        grpcChannelSingleton.CreateChannel(host, password);
        //    }
        //}

        PauseTokenSource.Resume();
    }

    public void OnActivityStopped(Activity activity)
    {
        Console.WriteLine("App WENT TO SLEEP");

        PauseTokenSource.Pause();

        AlarmUtils.ScheduleExactAlarm(Android.App.Application.Context);

        //if (SecureStorageHelper.Get<bool>("notifications-enabled"))
        //{
        //    AlarmUtils.ScheduleExactAlarm(Android.App.Application.Context);
        //}
    }
}
