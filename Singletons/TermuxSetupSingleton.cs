#if ANDROID

using Android.App;
using Android.Content;
using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Haveno.Proto.Grpc;
using Manta.Helpers;
using System.Runtime.InteropServices;
using System.Text;

using static Haveno.Proto.Grpc.GetVersion;

namespace Manta.Singletons;

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

// TODO
// Handle daemon/termux crash. Restart, reconnect
// Or if termux gets closed etc 
// If i can get RUN_COMMAND to work with output return we solve a lot of permission issues and make the install experience better
public class TermuxSetupSingleton
{
    private readonly Context _context = Android.App.Application.Context;
    private readonly string _fileName = "stdout";
    private Task? _havenoDaemonTask;
    private Task? _stdoutListenerTask;

    private const int _termuxStartWaitTime = 1_100;

    public event Action<int>? InstallationStep;

    public TermuxSetupSingleton()
    {

    }

    public async Task<bool> GetIsTermuxAndDaemonInstalledAsync()
    {
        return (await SecureStorageHelper.GetAsync<bool>("termux-installed")) && (await SecureStorageHelper.GetAsync<bool>("termux-setup"));
    }

    public async Task StopLocalHavenoDaemonAsync()
    {
        await ExecuteCommandAsync("\"killall -9 -e tor\"", true, millisecondsTimeout: 100);
        await ExecuteCommandAsync("\"killall -9 haveno\"", true, millisecondsTimeout: 100);
    }

    private async Task ToggleApps()
    {
        await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");

        var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent is not null)
        {
            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
        }

        await Task.Delay(_termuxStartWaitTime);

        await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");
    }

    private async Task ListenStdout(List<(string, Action)>? callbacks = null)
    {
        var stdout = Path.Combine(Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? throw new DirectoryNotFoundException("ExternalStorageDirectory"), "/storage/emulated/0/output", _fileName);

        using var fs = new FileStream(stdout, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);

        // Trim stdout file
        var currentContent = await reader.ReadToEndAsync();
        var linesToKeep = currentContent.Split('\n').TakeLast(100);
        var newContent = string.Join("\n", linesToKeep);

        fs.SetLength(0); 
        await fs.WriteAsync(Encoding.UTF8.GetBytes(newContent));
        await fs.FlushAsync();

        while (true)
        {
            var line = await reader.ReadLineAsync();

            if (line is not null)
            {
                Console.WriteLine(line);

                if (callbacks is not null && callbacks.Count != 0)
                {
                    foreach (var callback in callbacks)
                    {
                        if (line.Contains(callback.Item1))
                        {
                            callback.Item2.Invoke();
                            // Only match one once per line
                            break;
                        }
                    }
                }
            }
            else
            {
                await Task.Delay(100);
            }
        }
    }

    public async Task<bool> IsHavenoDaemonRunningAsync(CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                // Will tell us its running but not if its initialized
                using var grpcChannelHelper = new GrpcChannelHelper();
                var client = new GetVersionClient(grpcChannelHelper.Channel);
                var response = await client.GetVersionAsync(new GetVersionRequest(), cancellationToken: cancellationToken);

                return true;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception e) // Catch correct exception TODO, ignore rate limit exception
            {

            }

            try
            {
                await Task.Delay(1000, cancellationToken: cancellationToken);
            }
            catch (TaskCanceledException)
            {
                throw;
            }
        }

        return false;
    }

    public void TryAttachStdout()
    {
        if (_stdoutListenerTask is null)
            _stdoutListenerTask = Task.Run(() => ListenStdout([("All connections lost", () => Console.WriteLine("LOST CONNECTION")), ("Established a new connection", () => Console.WriteLine("RECONNECTED"))]));
    }

    public async Task CloseTermux()
    {
        await ExecuteTermuxCommandAsync("am stopservice --user 0 -n com.termux/.app.TermuxService");
    }

    // Issues with starting tor recently
    public async Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host)
    {
        try
        {
            // Since this is connection based, it could be running but unreachable
            if (await IsHavenoDaemonRunningAsync())
            {
                TryAttachStdout();
                return true;
            } 

            await SecureStorageHelper.SetAsync("password", password);
            await SecureStorageHelper.SetAsync("host", host);

            // channel helper should probably just pull this itself so we dont have to set it
            GrpcChannelHelper.Password = password;
            GrpcChannelHelper.Host = host;

            var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
            if (intent is not null)
            {
                intent.AddFlags(ActivityFlags.NewTask);
                _context.StartActivity(intent);
            }

            await Task.Delay(2_000);

            await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");

            await StopLocalHavenoDaemonAsync();

            var startScript = $"#!/bin/sh\r\n ./haveno-daemon --baseCurrencyNetwork=XMR_STAGENET --useLocalhostForP2P=false --useDevPrivilegeKeys=false --nodePort=9999 --appName=haveno-XMR_STAGENET_user1 --apiPassword={password} --apiPort=3201 --passwordRequired=false --useNativeXmrWallet=false --torControlHost=127.0.0.1 --torControlPort=9051";
            //var startScript = $"#!/bin/sh\r\n ./haveno-daemon --seedNodes=10.0.2.2:2002 --baseCurrencyNetwork=XMR_LOCAL --useLocalhostForP2P=true --useDevPrivilegeKeys=true --nodePort=7778 --appName=haveno-XMR_LOCAL_user3 --apiPassword={password} --apiPort=3201 --xmrNode=http://10.0.2.2:28081 --walletRpcBindPort=38093 --passwordRequired=false --useNativeXmrWallet=false";

            await ExecuteCommandAsync("\"rm -f haveno/start.sh\"", true);
            await ExecuteCommandAsync("\"touch haveno/start.sh\"", true);
            await ExecuteCommandAsync($"\"printf '{startScript}' > haveno/start.sh\"", true);
            await ExecuteCommandAsync($"\"chmod +x haveno/start.sh\"", true);

            //await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");

            // TODO Don't run as root, everything on termux seems to run as root, set up user with less perms
            // Send SIGINTs to both of these 
            await ExecuteCommandAsync("\"tor\"", true, exitOnContainsString: "100%");
            //"Consensus not signed by sufficient number of requested authorities"
            // Also runs as root - fix. Limit directory access
            // TODO error handling
            _havenoDaemonTask = Task.Run(() => ExecuteCommandAsync($"\"cd haveno && ./start.sh\"", true));

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);

            return false;
        }
    }

    public async Task<bool> RequestRequiredPermissionsAsync()
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

    public async Task CheckForHavenoUpdate()
    {

    }

    private async Task WaitForStoragePermissionAsync()
    {
        using CancellationTokenSource cancellationTokenSource = new(60_000);

        while (true)
        {
            try
            {
                if (cancellationTokenSource.Token.IsCancellationRequested)
                    return;

                var fileName = Guid.NewGuid().ToString();

                await ExecuteTermuxCommandAsync("termux-setup-storage", millisecondsTimeout: 2000);
                await ExecuteTermuxCommandAsync($"touch /storage/emulated/0/output/{fileName}");

                Directory.CreateDirectory(Path.Combine(Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? throw new DirectoryNotFoundException("ExternalStorageDirectory"), "/storage/emulated/0/output"));
                var file = Path.Combine(Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? throw new DirectoryNotFoundException("ExternalStorageDirectory"), "/storage/emulated/0/output", fileName);

                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);

                return;
            }
            catch (FileNotFoundException)
            {

            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch
            {
                throw;
            }
        }
    }

    public async Task SetupTermuxAsync()
    {
        DeviceDisplay.KeepScreenOn = true;

        InstallationStep?.Invoke(1);

        var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent is not null)
        {
            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
        }

        await Task.Delay(1_500);

        await ExecuteTermuxCommandAsync("termux-reload-settings");
        await ExecuteTermuxCommandAsync("pkg install termux-am", 15_000);
        await ExecuteTermuxCommandAsync("termux-reload-settings");

        // TODO remove notification permission from termux

        await WaitForStoragePermissionAsync();

        await ExecuteTermuxCommandAsync("termux-reload-settings");
        
        await ExecuteTermuxCommandAsync("am stopservice --user 0 -n com.termux/.app.TermuxService");

        await Task.Delay(2_000);

        var intent2 = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent2 is not null)
        {
            intent2.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent2);
        }

        await Task.Delay(_termuxStartWaitTime);

        await ExecuteTermuxCommandAsync("termux-wake-lock", 10_000);
        await ExecuteTermuxCommandAsync("termux-reload-settings");

        InstallationStep?.Invoke(2);
        await ToggleApps();

        await ExecuteCommandAsync("yes | pkg update -y");
        await ExecuteCommandAsync("yes | pkg upgrade -y");
        await ExecuteCommandAsync("pkg install proot-distro -y");
        await ExecuteCommandAsync("proot-distro add ubuntu", exitOnContainsString: "is already installed");

        InstallationStep?.Invoke(3);
        await ToggleApps();

        var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? throw new DirectoryNotFoundException("ExternalStorageDirectory"), "/storage/emulated/0/output", "ubuntu_exec.sh");

        using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var writer = new StreamWriter(fs);

        await writer.WriteAsync("#!/bin/bash\r\n# This script runs commands inside Ubuntu PRoot\r\nCOMMAND=\"$*\"  # Takes all arguments as the command\r\n\r\n# Execute inside Ubuntu\r\nproot-distro login ubuntu -- bash -c \"$COMMAND\"");
        writer.Close();

        await ExecuteCommandAsync("chmod +x /sdcard/output/ubuntu_exec.sh");

        await ToggleApps();

        // Now update Ubuntu
        await ExecuteCommandAsync("\"apt update && apt upgrade -y\"", true);
        await ExecuteCommandAsync("\"apt clean\"", true);
        await ExecuteCommandAsync("\"apt update\"", true);

        InstallationStep?.Invoke(4);
        await ToggleApps();

        // I don't know why but if you run this twice it works the second time
        await ExecuteCommandAsync("\"apt install openjdk-21-jdk -y\"", true, exitOnContainsString: "maybe run apt-get update or try with --fix-missing");
        await ExecuteCommandAsync("\"apt install openjdk-21-jdk -y\"", true);
        await ExecuteCommandAsync("\"apt install wget -y\"", true);
        await ExecuteCommandAsync("\"apt install unzip -y\"", true);

        InstallationStep?.Invoke(5);
        await ToggleApps();

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        string package = isWindows ? "windows" : "linux-";
        if (!isWindows)
        {
            package += RuntimeInformation.OSArchitecture.ToString() == "X64" ? "x86_64" : "aarch64";
        }

        await ExecuteCommandAsync("\"rm -fv -r haveno\"", true);
        await ExecuteCommandAsync($"\"wget https://github.com/atsamd21/haveno/releases/download/v1.1.1/{package}.zip\"", true);
        await ExecuteCommandAsync($"\"unzip {package}.zip\"", true);
        await ExecuteCommandAsync($"\"rm {package}.zip\"", true);
        await ExecuteCommandAsync($"\"mv {package} haveno\"", true);
        await ExecuteCommandAsync($"\"chmod +x haveno/haveno-daemon\"", true);

        InstallationStep?.Invoke(6);
        await ToggleApps();

        await ExecuteCommandAsync("\"apt install tor -y\"", true);
        await ExecuteCommandAsync("\"sed -i 's/#ControlPort/ControlPort/g' /etc/tor/torrc\"", true);

        await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");

        DeviceDisplay.KeepScreenOn = false;
    }

    private async Task ExecuteTermuxCommandAsync(string command, int millisecondsTimeout = 200)
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

    private async Task ExecuteCommandAsync(
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