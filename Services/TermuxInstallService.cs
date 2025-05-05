#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using System.Runtime.InteropServices;

namespace Manta.Services;

[Activity(Label = "Termux installer", LaunchMode = LaunchMode.SingleTop)]
public class TermuxInstallService : Activity
{
    private static TaskCompletionSource? _taskCompletionSource;
    public static Action<double>? OnProgressPercentageChange;

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

    public static async Task<byte[]> DownloadWithProgressAsync(string url, IProgress<double> progress, HttpClient httpClient)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength ?? -1L;
        long totalRead = 0;
        var buffer = new byte[8192];

        using var stream = await response.Content.ReadAsStreamAsync();

        using var ms = new MemoryStream();

        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            await ms.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;

            if (contentLength > 0)
                progress?.Report((double)totalRead / contentLength * 100);
        }

        return ms.ToArray();
    }

    public static void Resumed()
    {
        if (!_taskCompletionSource?.Task.IsCompleted ?? false)
            _taskCompletionSource?.SetResult();
    }

    public static async Task<(bool, string?)> InstallTermuxAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = Timeout.InfiniteTimeSpan;

            var progress = new Progress<double>(x =>
            {
                OnProgressPercentageChange?.Invoke(x);
            });

            var package = RuntimeInformation.OSArchitecture.ToString() == "X64" ? "x86_64" : "arm64-v8a";

            // Download custom Termux APK with allow-external-apps set to true by default
            //var apkBytes = await DownloadWithProgressAsync($"https://github.com/atsamd21/termux-app/releases/download/v0.1.2/termux-app_apt-android-7-release_{package}.apk", progress, httpClient);

            var apkBytes = await httpClient.GetByteArrayAsync($"https://github.com/atsamd21/termux-app/releases/download/v0.1.2/termux-app_apt-android-7-release_{package}.apk");

            string apkPath = Path.Combine(FileSystem.Current.AppDataDirectory, "termux.apk");
            await File.WriteAllBytesAsync(apkPath, apkBytes);

            var apkFile = new Java.IO.File(apkPath);
            var apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(Android.App.Application.Context, "[Manta.Application].fileprovider", apkFile);

            var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);

            _taskCompletionSource = new();

            Android.App.Application.Context.StartActivity(intent);

            await Task.Run(async () =>
            {
                while (!IsTermuxInstalled())
                {
                    await Task.Delay(1000);
                }
            });

            await _taskCompletionSource.Task;

            apkFile.Delete();

            return (true, null);
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
    }
}

#endif