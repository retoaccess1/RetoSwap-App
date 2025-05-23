#if ANDROID

using Android.Content;
using Grpc.Core;
using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Helpers;
using Manta.Services;
using System.Text;

namespace Manta.Singletons;

public class TermuxSetupSingleton
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Context _context = Android.App.Application.Context;

    private const int _termuxStartWaitTime = 1_100;

    public event Action<string>? OnTorStartInfo;
    public event Action<int>? InstallationStep;

    private readonly GrpcChannelSingleton _grpcChannelSingleton;

    public TermuxSetupSingleton(IServiceProvider serviceProvider, GrpcChannelSingleton grpcChannelSingleton)
    {
        _grpcChannelSingleton = grpcChannelSingleton;
        _serviceProvider = serviceProvider;
    }

    public Task<bool> GetIsTermuxAndDaemonInstalledAsync()
    {
        return SecureStorageHelper.GetAsync<bool>("termux-installed");
    }

    public async Task StopLocalHavenoDaemonAsync()
    {
        var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent is null)
            return;

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

    // This method should be available to all platforms
    public async Task<bool> IsHavenoDaemonRunningAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var versionService = _serviceProvider.GetRequiredService<IHavenoVersionService>();

        for (int i = 0; i < 2; i++)
        {
            try
            {
                // Will tell us its running but not if its initialized
                await versionService.GetVersionAsync(cancellationToken: cancellationToken);

                return true;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (RpcException)
            {

            }
            catch (Exception)
            {

            }

            try
            {
                if (i == 1)
                {
                    break;
                }

                await Task.Delay(1000, cancellationToken: cancellationToken);
            }
            catch (TaskCanceledException)
            {
                throw;
            }
        }

        return false;
    }

    // This one as well
    public async Task<bool> WaitHavenoDaemonInitializedAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var accountService = _serviceProvider.GetRequiredService<IHavenoAccountService>();

        while(!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var isAppInitialized = await accountService.IsAppInitializedAsync(cancellationToken: cancellationToken);
                if (isAppInitialized)
                    return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (RpcException)
            {

            }
            catch (Exception)
            {

            }

            try
            {
                await Task.Delay(1000, cancellationToken: cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    public async Task CloseTermux()
    {
        var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
        if (intent is null)
            return;

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

    private async Task PollTorStatus()
    {
        while (true)
        {
            var output = await ExecuteUbuntuCommandAsync("cat /data/data/com.termux/files/home/tor.log");
            if (string.IsNullOrEmpty(output))
            {
                continue;
            }

            StringBuilder stringBuilder = new();
            var indexOfLastPercent = output.LastIndexOf('%');
            for (int i = indexOfLastPercent; i > -1; i--)
            {
                if (output[i] == ' ')
                {
                    for (int j = i; output[j] != '%'; j++)
                    {
                        stringBuilder.Append(output[j]);
                    }

                    break;
                }
            }

            if (stringBuilder.Length > 0)
            {
                var percentage = int.Parse(stringBuilder.ToString());
                OnTorStartInfo?.Invoke("Bootstrapping: " + percentage.ToString() + "%");

                if (percentage == 100)
                {
                    return;
                }
            }
        }
    }

    public async Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host)
    {
        try
        {
            // Might just need to wake Termux up?
            // Since this is connection based, it could be running but unreachable
            if (await IsHavenoDaemonRunningAsync())
            {
                return true;
            }

            await SecureStorageHelper.SetAsync("password", password);
            await SecureStorageHelper.SetAsync("host", host);

            _grpcChannelSingleton.CreateChannel(host, password);

            var intent = _context.PackageManager?.GetLaunchIntentForPackage("com.termux");
            if (intent is not null)
            {
                intent.AddFlags(ActivityFlags.NewTask);
                _context.StartActivity(intent);
            }

            await Task.Delay(2_000);

            await ExecuteTermuxCommandAsync("am start -a android.intent.action.VIEW -d \"manta://termux_callback\"");

            await StopLocalHavenoDaemonAsync();

            string baseCurrencyNetwork;
#if DEBUG
            baseCurrencyNetwork = "XMR_STAGENET";
#else
            baseCurrencyNetwork = "XMR_MAINNET";
#endif

            //await ExecuteUbuntuCommandAsync("tor --Log 'notice file /data/data/com.termux/files/home/tor.log'");
            //_ = Task.Run(() => ExecuteUbuntuCommandAsync("tor --Log 'notice file /data/data/com.termux/files/home/tor.log'"));

            ExecuteUbuntuCommand("rm /data/data/com.termux/files/home/tor.log");
            await Task.Delay(50);

            ExecuteUbuntuCommand("tor --Log 'notice file /data/data/com.termux/files/home/tor.log'");

            await Task.Delay(100);

            await PollTorStatus();

            ExecuteUbuntuCommand($"cd haveno && ./haveno-daemon --baseCurrencyNetwork=XMR_STAGENET --useLocalhostForP2P=false --useDevPrivilegeKeys=false --nodePort=9999 --appName=haveno-XMR_STAGENET_user1 --apiPassword={password} --apiPort=3201 --passwordRequired=false --useNativeXmrWallet=false --torControlHost=127.0.0.1 --torControlPort=9051");

            //_ = Task.Run(() => ExecuteUbuntuCommandAsync($"tor"));
            //_ = Task.Run(() => ExecuteUbuntuCommandAsync($"cd haveno && ./haveno-daemon --baseCurrencyNetwork=XMR_STAGENET --useLocalhostForP2P=false --useDevPrivilegeKeys=false --nodePort=9999 --appName=haveno-XMR_STAGENET_user1 --apiPassword={password} --apiPort=3201 --passwordRequired=false --useNativeXmrWallet=false --torControlHost=127.0.0.1 --torControlPort=9051"));

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

    public async Task<bool> RequestEnableWakeLockAsync()
    {
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

    public void ExecuteUbuntuCommand(string command)
    {
        ExecuteTermuxCommand($"bash $PREFIX/bin/ubuntu_exec \"{command}\"");
    }

    public void ExecuteTermuxCommand(string command)
    {
        var intent = new Intent("com.termux.RUN_COMMAND");
        intent.SetClassName("com.termux", "com.termux.app.RunCommandService");

        intent.PutExtra("com.termux.RUN_COMMAND_PATH", "/data/data/com.termux/files/usr/bin/bash");

        intent.PutExtra("com.termux.RUN_COMMAND_ARGUMENTS", ["-c", command]);

        intent.PutExtra("com.termux.RUN_COMMAND_WORKDIR", "/data/data/com.termux/files/home");

        intent.PutExtra("com.termux.RUN_COMMAND_BACKGROUND", true);
        intent.PutExtra("com.termux.RUN_COMMAND_SESSION_ACTION", "0");

        _context.StartService(intent);
    }
}

#endif