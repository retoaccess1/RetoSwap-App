using Android.App;
using Android.OS;
using Manta.Helpers;

namespace Manta.Services;

public class AppLifecycleService : Java.Lang.Object, Android.App.Application.IActivityLifecycleCallbacks
{
    public void OnActivityCreated(Activity activity, Bundle? savedInstanceState) { }

    public void OnActivityDestroyed(Activity activity) { }

    public void OnActivityPaused(Activity activity) { }

    public void OnActivityResumed(Activity activity) { }

    public void OnActivitySaveInstanceState(Activity activity, Bundle outState) { }

    public void OnActivityStarted(Activity activity)
    {
        Console.WriteLine("App RESUMED from background");

        AlarmUtils.CancelAlarm(Android.App.Application.Context);
    }

    public void OnActivityStopped(Activity activity)
    {
        Console.WriteLine("App WENT TO SLEEP");

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
