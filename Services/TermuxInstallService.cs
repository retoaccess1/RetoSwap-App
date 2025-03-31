#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;

namespace Manta.Services;

[Activity(Label = "Termux installer", LaunchMode = LaunchMode.SingleTop)]
public class TermuxInstallService : Activity
{
    public static bool IsTermuxInstalled()
    {
        var pm = Android.App.Application.Context.PackageManager;
        try
        {
            var packageInfo = pm?.GetPackageInfo("com.termux", PackageInfoFlags.Activities);
            return packageInfo is not null;
        }
        catch
        {
            return false;
        }
    }

    public static async Task InstallTermuxAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = Timeout.InfiniteTimeSpan;

            // Download custom Termux APK with allow-external-apps set to true by default
            var apkBytes = await httpClient.GetByteArrayAsync("https://github.com/atsamd21/termux-app/releases/download/0.0.3/termux-app_apt-android-7-release_universal.apk");

            string apkPath = Path.Combine(FileSystem.Current.AppDataDirectory, "termux.apk");
            await File.WriteAllBytesAsync(apkPath, apkBytes);

            var apkFile = new Java.IO.File(apkPath);
            var apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(Android.App.Application.Context, "[Manta.Application].fileprovider", apkFile);

            var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);

            Android.App.Application.Context.StartActivity(intent);

            await Task.Run(async () =>
            {
                while (!IsTermuxInstalled())
                {
                    await Task.Delay(1000);
                }
            });
        }
        catch (Exception e)
        {

        }
    }
}

#endif