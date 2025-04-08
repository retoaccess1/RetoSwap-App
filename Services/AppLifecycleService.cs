#if ANDROID

using Android.App;
using Android.OS;
using AndroidX.Work;

namespace Manta.Services;

public class AppLifecycleService : Java.Lang.Object, Android.App.Application.IActivityLifecycleCallbacks
{
    private string? _workName;

    public void OnActivityCreated(Activity activity, Bundle? savedInstanceState) { }

    public void OnActivityDestroyed(Activity activity) { }

    public void OnActivityPaused(Activity activity) { }

    public void OnActivityResumed(Activity activity) { }

    public void OnActivitySaveInstanceState(Activity activity, Bundle outState) { }

    public void OnActivityStarted(Activity activity)
    {
        Console.WriteLine("App RESUMED from background");

        if (_workName is not null)
        {
            WorkManager.GetInstance(Android.App.Application.Context).CancelUniqueWork(_workName);
            _workName = null;
        }
    }

    public void OnActivityStopped(Activity activity)
    {
        Console.WriteLine("App WENT TO SLEEP");

        // Cancel if already running
        if (_workName is not null)
        {
           WorkManager.GetInstance(Android.App.Application.Context).CancelUniqueWork(_workName);
        }

        _workName = $"daemon_fetch_work_{Guid.NewGuid()}";

        var constraints = new Constraints.Builder()
            .SetRequiredNetworkType(NetworkType.Connected)
            .Build();

        //// Would be nice to start this when the app is in the bg, maybe use a semaphore to make the other service wait and release that lock when app goes into active use
        //// 15 mins is the minimum
        //var workRequest = (PeriodicWorkRequest)PeriodicWorkRequest.Builder
        //    .From<BackgroundWorker>(TimeSpan.FromMinutes(15))
        //    .SetConstraints(constraints)
        //    .SetInitialDelay(2, Java.Util.Concurrent.TimeUnit.Minutes)
        //    .Build();

        //WorkManager.GetInstance(Android.App.Application.Context).EnqueueUniquePeriodicWork(
        //    _workName,
        //    ExistingPeriodicWorkPolicy.Keep,
        //    workRequest
        //);
    }
}

#endif