using Android.App;
using Android.App.Job;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Manta.Services;
using Microsoft.Maui.Controls;

namespace Manta;
[Activity(LaunchMode = LaunchMode.SingleTop, ScreenOrientation = ScreenOrientation.Portrait, Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Intent.ActionView],
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "manta",
    DataHost = "termux_callback")]
public class MainActivity : MauiAppCompatActivity
{
    public MainActivity()
    {
        //var jobScheduler = (JobScheduler)GetSystemService(JobSchedulerService);
        //var builder = new JobInfo.Builder(1, new ComponentName(this, Java.Lang.Class.FromType(typeof(MyBackgroundJob))));
        //// Every 15 min
        //builder.SetPeriodic(5 * 60 * 1000); 
        //jobScheduler.Schedule(builder.Build());
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode == StorageHelper.RequestCode)
            StorageHelper.OnActivityResult();

        base.OnActivityResult(requestCode, resultCode, data);
    }

    private void HandleIntent(Intent intent)
    {
        if (intent?.Data?.Host == "termux_callback")
        {
            Console.WriteLine("Returned from Termux!");
        }
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        
        if (Intent is not null)
            HandleIntent(Intent);

        Window?.SetNavigationBarColor(Android.Graphics.Color.ParseColor("#131313"));

        if (Intent is null)
            return;

        CreateNotificationFromIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        if (intent is null)
            return;

        CreateNotificationFromIntent(intent);
        HandleIntent(intent);
    }

    static void CreateNotificationFromIntent(Intent intent)
    {
        if (intent?.Extras != null)
        {
            var title = intent.GetStringExtra(NotificationManagerService.TitleKey);
            var message = intent.GetStringExtra(NotificationManagerService.MessageKey);
            if (title is null || message is null)
                return;

            var service = IPlatformApplication.Current?.Services.GetService<INotificationManagerService>();
            if (service is null)
                return;

            service.ReceiveNotification(title, message);
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        TermuxPermissionHelper.HandlePermissionResult(requestCode, permissions, grantResults);
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    }


}
