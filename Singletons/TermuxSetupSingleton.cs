#if ANDROID

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Services;

using static Haveno.Proto.Grpc.GetVersion;

namespace Manta.Singletons;

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

public class TermuxSetupSingleton
{
    private readonly Context _context = Android.App.Application.Context;

    private const int _termuxStartWaitTime = 1_100;

    public event Action<int>? InstallationStep;

    public TermuxSetupSingleton()
    {

    }

    public Task<bool> GetIsTermuxAndDaemonInstalledAsync()
    {
        return SecureStorageHelper.GetAsync<bool>("termux-installed");
    }

    public async Task StopLocalHavenoDaemonAsync()
    {
        await ExecuteUbuntuCommandAsync("killall -9 -e tor");
        await ExecuteUbuntuCommandAsync("killall -9 haveno");
    }

    public async Task ToggleApps()
    {
        var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent is not null)
        {
            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
        }

        await Task.Delay(_termuxStartWaitTime);

        await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");
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

    public async Task CloseTermux()
    {
        await ExecuteTermuxCommandAsync("am stopservice --user 0 -n com.termux/.app.TermuxService");
    }

    public async Task OpenTermux()
    {
        var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent is not null)
        {
            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
        }

        await Task.Delay(_termuxStartWaitTime);
    }

    public async Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host)
    {
        try
        {
            //await ToggleApps();

            // Might just need to wake Termux up?
            // Since this is connection based, it could be running but unreachable
            if (await IsHavenoDaemonRunningAsync())
            {
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

            _ = Task.Run(() => ExecuteUbuntuCommandAsync($"tor"));
            _ = Task.Run(() => ExecuteUbuntuCommandAsync($"cd haveno && ./haveno-daemon --baseCurrencyNetwork=XMR_STAGENET --useLocalhostForP2P=false --useDevPrivilegeKeys=false --nodePort=9999 --appName=haveno-XMR_STAGENET_user1 --apiPassword={password} --apiPort=3201 --passwordRequired=false --useNativeXmrWallet=false --torControlHost=127.0.0.1 --torControlPort=9051"));

            //_ = Task.Run(() => ExecuteUbuntuCommandAsync($"cd haveno && sh start.sh XMR_STAGENET XMR_STAGENET_user1 {password}"));

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);

            return false;
        }
    }

    public async Task CheckForHavenoUpdate()
    {

    }

    public async Task SetupTermuxAsync()
    {
        var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent is not null)
        {
            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
        }

        await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");
    }

    public async Task<bool> RequestEnableWakeLockAsync()
    {
        //await CloseTermux();

        var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent is not null)
        {
            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
        }

        try
        {
            using CancellationTokenSource cancellationTokenSource = new(5_000);

            await ExecuteTermuxCommandAsync("termux-wake-lock", cancellationTokenSource.Token);
            await ExecuteTermuxCommandAsync("termux-reload-settings", cancellationTokenSource.Token);
        }
        catch
        {

        }

        await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");

        return true;
    }

    public Task<string?> ExecuteTermuxCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        return PluginResultsService.ExecuteTermuxCommandAsync(command, cancellationToken);
    }

    public Task<string?> ExecuteUbuntuCommandAsync(string command)
    {
        return PluginResultsService.ExecuteUbuntuCommandAsync(command);
    }

    //public Task<string?> ExecuteTermuxCommandAsync(string command)
    //{
    //    var intent = new Intent("com.termux.RUN_COMMAND");
    //    intent.SetClassName("com.termux", "com.termux.app.RunCommandService");

    //    intent.PutExtra("com.termux.RUN_COMMAND_PATH", "/data/data/com.termux/files/usr/bin/bash");

    //    intent.PutExtra("com.termux.RUN_COMMAND_ARGUMENTS", ["-c", command]);

    //    intent.PutExtra("com.termux.RUN_COMMAND_WORKDIR", "/data/data/com.termux/files/home");

    //    intent.PutExtra("com.termux.RUN_COMMAND_BACKGROUND", true);
    //    intent.PutExtra("com.termux.RUN_COMMAND_SESSION_ACTION", "0");

    //    var pluginResultsServiceIntent = new Intent(_context, typeof(PluginResultsService));
    //    var executionId = PluginResultsService.GetNextExecutionId();
    //    PluginResultsService.TaskCompletionSource = new();

    //    pluginResultsServiceIntent.PutExtra(PluginResultsService.ExecutionId, executionId);

    //    var pendingIntent = PendingIntent.GetService(_context,
    //        executionId,
    //        pluginResultsServiceIntent,
    //        PendingIntentFlags.OneShot | (Build.VERSION.SdkInt >= BuildVersionCodes.S ? PendingIntentFlags.Mutable : 0));

    //    intent.PutExtra("com.termux.RUN_COMMAND_PENDING_INTENT", pendingIntent);

    //    _context.StartService(intent);

    //    return PluginResultsService.TaskCompletionSource.Task;
    //}

    //public Task<string?> ExecuteUbuntuCommandAsync(string command)
    //{
    //    return ExecuteTermuxCommandAsync($"bash $PREFIX/bin/ubuntu_exec \"{command}\"");
    //}
}

#endif