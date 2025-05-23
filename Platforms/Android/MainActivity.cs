using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;
using Manta.Platforms.Android.Services;
using Manta.Services;

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
        _ = typeof(Manta.Services.TermuxReceiver);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
    }

    private void HandleIntent(Intent intent)
    {
        if (intent?.Data?.Host == "termux_callback")
        {
            Console.WriteLine("Returned from Termux!");
            var output = intent.Data.GetQueryParameter("result");
        }

        // TODO Handle notification. if message, navigate to chat, if trade navigate to trade
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Window?.SetNavigationBarColor(Android.Graphics.Color.ParseColor("#000000"));

        var rootView = Window?.DecorView?.RootView;
        if (rootView != null)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(rootView, new InsetsListener());
        }

        if (Intent is not null)
            HandleIntent(Intent);


        if (Intent is null)
            return;

        CreateNotificationFromIntent(Intent);

#if ANDROID29_0_OR_GREATER
        RegisterActivityLifecycleCallbacks(new AppLifecycleService());
#endif
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
            var title = intent.GetStringExtra(AndroidNotificationManagerService.TitleKey);
            var message = intent.GetStringExtra(AndroidNotificationManagerService.MessageKey);
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

    protected override void OnResume()
    {
        base.OnResume();
        TermuxInstallService.Resumed();
    }
}