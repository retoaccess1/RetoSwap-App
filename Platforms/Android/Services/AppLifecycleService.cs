using Android.App;
using Android.OS;
using HavenoSharp.Singletons;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;

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

        var serviceProvider = IPlatformApplication.Current?.Services;
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

        var daemonConnectionSingleton = serviceProvider?.GetService<DaemonConnectionSingleton>();
        var tradeStatisticsSingleton = serviceProvider?.GetService<TradeStatisticsSingleton>();
        var daemonInfoSingleton = serviceProvider?.GetService<DaemonInfoSingleton>();
        var balanceSingleton = serviceProvider?.GetService<BalanceSingleton>();

        daemonConnectionSingleton?.Resume();
        tradeStatisticsSingleton?.Resume();
        daemonInfoSingleton?.Resume();
        balanceSingleton?.Resume();
    }

    public void OnActivityStopped(Activity activity)
    {
        Console.WriteLine("App WENT TO SLEEP");

        var serviceProvider = IPlatformApplication.Current?.Services;

        var daemonConnectionSingleton = serviceProvider?.GetService<DaemonConnectionSingleton>();
        var tradeStatisticsSingleton = serviceProvider?.GetService<TradeStatisticsSingleton>();
        var daemonInfoSingleton = serviceProvider?.GetService<DaemonInfoSingleton>();
        var balanceSingleton = serviceProvider?.GetService<BalanceSingleton>();

        daemonConnectionSingleton?.Pause();
        tradeStatisticsSingleton?.Pause();
        daemonInfoSingleton?.Pause();
        balanceSingleton?.Pause();

        // This probably isnt as needed now that the daemon runs in the app
        // But still might want to use this a battery optimization
        // Can we even get trade/chat updates when app is in sleep?  
        // TODO: Need to pause all polling singletons when app goes into bg
        if (SecureStorageHelper.Get<bool>("notifications-enabled"))
        {
            AlarmUtils.ScheduleExactAlarm(Android.App.Application.Context);
        }
    }
}
