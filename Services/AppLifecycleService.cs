#if ANDROID

using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Work;

namespace Manta.Services;

public class AppLifecycleService : Java.Lang.Object, Android.App.Application.IActivityLifecycleCallbacks
{
    private readonly string[] _workNames = new string[1];

    public void OnActivityCreated(Activity activity, Bundle? savedInstanceState) { }

    public void OnActivityDestroyed(Activity activity) { }

    public void OnActivityPaused(Activity activity) { }

    public void OnActivityResumed(Activity activity) { }

    public void OnActivitySaveInstanceState(Activity activity, Bundle outState) { }

    public void OnActivityStarted(Activity activity)
    {
        Console.WriteLine("App RESUMED from background");

        //WorkManager.GetInstance(Android.App.Application.Context).CancelAllWork();

        //AlarmUtils.CancelAlarm(Android.App.Application.Context);
    }

    public void OnActivityStopped(Activity activity)
    {
        Console.WriteLine("App WENT TO SLEEP");

        //WorkManager.GetInstance(Android.App.Application.Context).CancelAllWork();

        //AlarmUtils.ScheduleExactAlarm(Android.App.Application.Context);

        //if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        //{
        //    var intent = new Intent(Android.Provider.Settings.ActionRequestScheduleExactAlarm);
        //    activity.StartActivity(intent);
        //}

        //for (int i = 0; i < _workNames.Length; i++)
        //{
        //    var workName = $"daemon_fetch_work_{Guid.NewGuid()}";

        //    var constraints = new Constraints.Builder()
        //        .SetRequiredNetworkType(NetworkType.NotRequired!)
        //        .Build();

        //    // Would be nice to start this when the app is in the bg, maybe use a semaphore to make the other service wait and release that lock when app goes into active use
        //    // 15 mins is the minimum
        //    var workRequest = (PeriodicWorkRequest)PeriodicWorkRequest.Builder
        //        .From<BackgroundWorker>(15, Java.Util.Concurrent.TimeUnit.Minutes!)
        //        .SetConstraints(constraints)
        //        .SetInitialDelay(5 * i, Java.Util.Concurrent.TimeUnit.Minutes)!
        //        .Build();

        //    WorkManager.GetInstance(Android.App.Application.Context).EnqueueUniquePeriodicWork(
        //        workName,
        //        ExistingPeriodicWorkPolicy.Keep!,
        //        workRequest
        //    );

        //    _workNames[i] = workName;
        //}
    }
}

#endif