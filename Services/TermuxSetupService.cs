#if ANDROID

using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Haveno.Proto.Grpc;
using Manta.Helpers;

using static Haveno.Proto.Grpc.GetVersion;

namespace Manta.Services;

public static class StorageHelper
{
    public const int RequestCode = 2296;
    private static TaskCompletionSource<bool>? GetPermissionTask { get; set; }

    public static async Task<bool> GetManageAllFilesPermission()
    {
        try
        {
            if (Android.OS.Environment.IsExternalStorageManager)
                return true;

            Android.Net.Uri? uri = Android.Net.Uri.Parse("package:" + Platform.CurrentActivity.ApplicationInfo.PackageName);

            GetPermissionTask = new();
            Intent intent = new(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission, uri);
            Platform.CurrentActivity.StartActivityForResult(intent, RequestCode);
        }
        catch (Exception ex)
        {

        }

        return await GetPermissionTask.Task;
    }

    public static void OnActivityResult()
    {
        GetPermissionTask?.SetResult(Android.OS.Environment.IsExternalStorageManager);
    }
}

public static class TermuxPermissionHelper
{
    private static TaskCompletionSource<bool> _tcs;
    private static readonly Activity _activity = Platform.CurrentActivity;

    public static async Task<bool> RequestRunCommandPermissionAsync()
    {
        if (ContextCompat.CheckSelfPermission(_activity, "com.termux.permission.RUN_COMMAND") == (int)Permission.Granted)
            return true;

        _tcs = new TaskCompletionSource<bool>();

        ActivityCompat.RequestPermissions(_activity, ["com.termux.permission.RUN_COMMAND"], 0);

        // Increase
        return await _tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public static void HandlePermissionResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (requestCode == 0 && _tcs != null)
        {
            bool granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            _tcs.TrySetResult(granted);
        }
    }
}

// If i can get RUN_COMMAND to work with output return we solve a lot of permission issues and make the install experience better
public class TermuxSetupService
{
    private static readonly Context _context = Android.App.Application.Context;
    private static readonly string _fileName = "stdout";

    public static event Action<int>? InstallationStep;

    public static Task? HavenoDaemonTask { get; private set; }
    public static Task? StoutListenerTask { get; private set; }

    public static TaskCompletionSource<bool> HavenoDaemonRunningTCS { get; private set; } = new();

    public static async Task RestartHavenoDaemon()
    {
        throw new NotImplementedException();

        await ExecuteCommandAsync("\"pkill -f tor\"", true, millisecondsTimeout: 200);
        await ExecuteCommandAsync("\"pkill -f haveno\"", true, millisecondsTimeout: 200);
        HavenoDaemonTask = Task.Run(() => ExecuteCommandAsync("\"cd haveno && make user1-daemon-stagenet\"", true));
    }

    public static async Task ToggleApps()
    {
        await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");

        var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent is not null)
        {
            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
        }

        await Task.Delay(1000);
    }

    private static async Task ListenStdout()
    {
        var stdout = Path.Combine(Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? throw new DirectoryNotFoundException("ExternalStorageDirectory"), "/storage/emulated/0/output", _fileName);

        using var fs = new FileStream(stdout, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);

        while (true)
        {
            var line = await reader.ReadLineAsync();

            if (line is not null)
            {
                Console.WriteLine(line);
            }
            else
            {
                await Task.Delay(100);
            }
        }
    }

    // TODO
    // Handle daemon/termux crash. Restart, reconnect
    // Or if termux gets closed etc 

    public static async Task<bool> IsHavenoDaemonRunning()
    {
        for (int i = 0; i < 2; i++)
        {
            try
            {
                using var grpcChannelHelper = new GrpcChannelHelper();
                var client = new GetVersionClient(grpcChannelHelper.Channel);

                var response = await client.GetVersionAsync(new GetVersionRequest());

                // Attach stdout listener, shared responsibility?
                if (StoutListenerTask is null)
                    StoutListenerTask = Task.Run(ListenStdout);

                return true;
            }
            catch (Exception e)    // Catch correct exception TODO, ignore rate limit exception
            {

            }

            await Task.Delay(1000);
        }

        return false;
    }

    public static async Task<bool> TryStartHavenoDaemon()
    {
        try
        {
            if (await IsHavenoDaemonRunning())
            {
                HavenoDaemonRunningTCS?.SetResult(true);
                return true;
            }

            var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
            if (intent is not null)
            {
                intent.AddFlags(ActivityFlags.NewTask);
                _context.StartActivity(intent);
            }

            await Task.Delay(1000);

            await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");
            // TODO Don't run as root, everything on termux seems to run as root, set up user with less perms
            await ExecuteCommandAsync("\"tor\"", true, exitOnContainsString: "100%");
            // Also runs as root - fix. Limit directory access
            // TODO error handling
            HavenoDaemonTask = Task.Run(() => ExecuteCommandAsync("\"cd haveno && make user1-daemon-stagenet\"", true));

            // Could loop forever
            while (!await IsHavenoDaemonRunning())
            {
                await Task.Delay(2000);
            }

            HavenoDaemonRunningTCS?.SetResult(true);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);

            return false;
        }
    }

    public static async Task<bool> RequestRequiredPermissions()
    {
        await TermuxPermissionHelper.RequestRunCommandPermissionAsync();

        if (ContextCompat.CheckSelfPermission(_context, "com.termux.permission.RUN_COMMAND") != (int)Permission.Granted)
            return false;

        // Legacy - fix if needed for older support
        //var StorageWriteStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
        //var StorageReadStatus = await Permissions.RequestAsync<Permissions.StorageRead>();

        await StorageHelper.GetManageAllFilesPermission();

        if (!Android.OS.Environment.IsExternalStorageManager)
            return false;

        await Task.Delay(2000);

        return true;
    }

    public static async Task UpdateTermux()
    {
        DeviceDisplay.KeepScreenOn = true;

        var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent is not null)
        {
            intent.AddFlags(ActivityFlags.NewTask);
            //intent.AddFlags(ActivityFlags.ExcludeFromRecents);
            //intent.AddFlags(ActivityFlags.PreviousIsTop);

            _context.StartActivity(intent);
        }

        await Task.Delay(1000);

        // Pray that it does not take longer than 15s...
        // Could maybe spin another polling thread? as this will probably time out for some users
        await ExecuteTermuxCommandAsync("termux-reload-settings");
        await ExecuteTermuxCommandAsync("termux-wake-lock");
        await ExecuteTermuxCommandAsync("termux-reload-settings");
        await ExecuteTermuxCommandAsync("pkg install termux-am", 15_000);
        //await ExecuteTermuxCommandAsync("am start --user 0 -a android.settings.action.MANAGE_OVERLAY_PERMISSION -d \"package:com.termux\"", 15_000);
        await ExecuteTermuxCommandAsync("termux-setup-storage", 10_000);
        await ExecuteTermuxCommandAsync("termux-reload-settings");
        await ExecuteTermuxCommandAsync("am startservice -a com.termux.service_stop com.termux/.app.TermuxService");
        await Task.Delay(500);

        var intent2 = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent2 is not null)
        {
            intent2.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent2);
        }

        await Task.Delay(1000);

        var intent3 = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent3 is not null)
        {
            intent3.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent3);
        }

        await Task.Delay(1000);

        // After this we should go back to haveno app
        //await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");

        // Update 
        await ExecuteCommandAsync("yes | pkg update -y");
        await ExecuteCommandAsync("yes | pkg upgrade -y");
        await ExecuteCommandAsync("pkg install proot-distro -y");
        await ExecuteCommandAsync("proot-distro add ubuntu", exitOnContainsString: "is already installed");

        var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? throw new DirectoryNotFoundException("ExternalStorageDirectory"), "/storage/emulated/0/output", "ubuntu_exec.sh");

        using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var writer = new StreamWriter(fs);

        await writer.WriteAsync("#!/bin/bash\r\n# This script runs commands inside Ubuntu PRoot\r\nCOMMAND=\"$*\"  # Takes all arguments as the command\r\n\r\n# Execute inside Ubuntu\r\nproot-distro login ubuntu -- bash -c \"$COMMAND\"");
        writer.Close();

        await ExecuteCommandAsync("chmod +x /sdcard/output/ubuntu_exec.sh");

        // Might help with stopping
        await ToggleApps();

        // Now update Ubuntu
        await ExecuteCommandAsync("\"apt update && apt upgrade -y\"", true);
        await ExecuteCommandAsync("\"apt clean\"", true);
        await ExecuteCommandAsync("\"apt update\"", true);
        // I don't know why but if you run this twice it works the second time
        await ExecuteCommandAsync("\"apt install openjdk-21-jdk -y\"", true, exitOnContainsString: "maybe run apt-get update or try with --fix-missing");
        await ExecuteCommandAsync("\"apt install openjdk-21-jdk -y\"", true);
        await ExecuteCommandAsync("\"apt install git -y\"", true);
        await ExecuteCommandAsync("\"apt install wget -y\"", true);
        await ExecuteCommandAsync("\"apt install make -y\"", true);

        await ExecuteCommandAsync("\"rm -fv -r haveno\"", true);
        await ExecuteCommandAsync("\"git clone https://github.com/haveno-dex/haveno.git\"", true);

        await ToggleApps();

        await ExecuteCommandAsync("\"cd haveno && make skip-tests\"", true);

        await ExecuteCommandAsync("\"apt install tor -y\"", true);
        await ExecuteCommandAsync("\"sed -i 's/#ControlPort/ControlPort/g' /etc/tor/torrc\"", true);
        // Custom file with torcontrol ports
        await ExecuteCommandAsync("\"cd haveno && wget -O Makefile https://raw.githubusercontent.com/atsamd21/Makefile/refs/heads/main/Makefile\"", true);

        await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");

        DeviceDisplay.KeepScreenOn = false;
    }

    public static async Task ExecuteTermuxCommandAsync(string command, int millisecondsTimeout = 200)
    {
        var intent = new Intent("com.termux.RUN_COMMAND");
        intent.SetClassName("com.termux", "com.termux.app.RunCommandService");

        intent.PutExtra("com.termux.RUN_COMMAND_PATH", "/data/data/com.termux/files/usr/bin/bash");

        intent.PutExtra("com.termux.RUN_COMMAND_ARGUMENTS", ["-c", command]);

        intent.PutExtra("com.termux.RUN_COMMAND_WORKDIR", "/data/data/com.termux/files/home");

        intent.PutExtra("com.termux.RUN_COMMAND_BACKGROUND", false);
        intent.PutExtra("com.termux.RUN_COMMAND_SESSION_ACTION", "0");

        _context.StartService(intent);

        await Task.Delay(millisecondsTimeout);
    }

    public static async Task ExecuteCommandAsync(
        string command, 
        bool isUbuntuCommand = false, 
        int millisecondsTimeout = 0, 
        string? exitOnContainsString = null, 
        bool dontWaitForExit = false)
    {
        try
        {
            CancellationTokenSource? cts = null;
            if (millisecondsTimeout != 0)
            {
                cts = new(millisecondsTimeout);
            }

            // "Unique" id
            var time = DateTime.Now.Ticks.ToString();

            // This file is going to get massive, do something about that
            Directory.CreateDirectory(Path.Combine(Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? throw new DirectoryNotFoundException("ExternalStorageDirectory"), "/storage/emulated/0/output"));
            var stdout = Path.Combine(Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? throw new DirectoryNotFoundException("ExternalStorageDirectory"), "/storage/emulated/0/output", _fileName);

            using var fs = new FileStream(stdout, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);

            var fullCommand = $"{(isUbuntuCommand ? "sh /sdcard/output/ubuntu_exec.sh" : "")} {command} &> /sdcard/output/{_fileName} && echo {time} >> /sdcard/output/{_fileName}";

            var intent = new Intent("com.termux.RUN_COMMAND");
            intent.SetClassName("com.termux", "com.termux.app.RunCommandService");

            intent.PutExtra("com.termux.RUN_COMMAND_PATH", "/data/data/com.termux/files/usr/bin/bash");

            intent.PutExtra("com.termux.RUN_COMMAND_ARGUMENTS", ["-c", fullCommand]);

            intent.PutExtra("com.termux.RUN_COMMAND_WORKDIR", "/data/data/com.termux/files/home");

            intent.PutExtra("com.termux.RUN_COMMAND_BACKGROUND", false);
            intent.PutExtra("com.termux.RUN_COMMAND_SESSION_ACTION", "0");

            _context.StartService(intent);

            while (true)
            {
                string? line;
                if (cts is not null)
                {
                    line = await reader.ReadLineAsync(cts.Token);
                }
                else
                {
                    line = await reader.ReadLineAsync();
                }

                if (line is not null)
                {
                    Console.WriteLine(line);

                    if ((!string.IsNullOrEmpty(exitOnContainsString)) && line.Contains(exitOnContainsString))
                    {
                        return;
                    }

                    if (line.Contains(time))
                    {
                        break;
                    }
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }
        catch (Exception ex)
        {

        }
    }
}

#endif