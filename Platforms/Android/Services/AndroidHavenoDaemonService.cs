using Android.OS;
using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Helpers;
using System.Runtime.InteropServices;
using System.Text;

namespace Manta.Services;

public class AndroidHavenoDaemonService : HavenoDaemonServiceBase
{
    private readonly GrpcChannelSingleton _grpcChannelSingleton;

    private static CancellationTokenSource? _torCts;
    private static CancellationTokenSource? _daemonCts;
    private static TaskCompletionSource? _torReadyTCS;
    private static PowerManager.WakeLock? _wakeLock;

    public AndroidHavenoDaemonService(
        GrpcChannelSingleton grpcChannelSingleton, 
        IServiceProvider serviceProvider, 
        IHavenoWalletService walletService, 
        IHavenoVersionService versionService, 
        IHavenoAccountService accountService
        ) : base(serviceProvider, walletService, versionService, accountService)
    {
        _grpcChannelSingleton = grpcChannelSingleton;
    }

    public override async Task InstallHavenoDaemonAsync()
    {
        var ubuntuDownloadStream = await Proot.DownloadUbuntu();
        await Proot.ExtractUbuntu(ubuntuDownloadStream);

        var arch = RuntimeInformation.OSArchitecture.ToString() == "X64" ? "amd64" : "arm64";

        Proot.RunProotUbuntuCommand("rm", "/bin/java");
        Proot.RunProotUbuntuCommand("ln", "-s", $"/usr/lib/jvm/java-21-openjdk-{arch}/bin/java", "/bin/java");
        Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/java.security", $"/usr/lib/jvm/java-21-openjdk-{arch}/conf/security/java.security");
        Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/java.policy", $"/usr/lib/jvm/java-21-openjdk-{arch}/conf/security/java.policy");
        Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/default.policy", $"/usr/lib/jvm/java-21-openjdk-{arch}/lib/security/default.policy");
        Proot.RunProotUbuntuCommand("chmod", "+x", "/usr/share/haveno/haveno-daemon");
    }

    public override async Task<bool> GetIsDaemonInstalledAsync()
    {
        try
        {
            var result = Proot.RunProotUbuntuCommand("echo", "check");
            if (!result.Contains("check"))
                return false;

            // TODO should check for actual version
            result = Proot.RunProotUbuntuCommand("java", "--version");
            if (!result.Contains("21"))
                return false;

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // TODO Add progress callback
    public override async Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host, Action<string>? progressCb = default)
    {
        if (await IsHavenoDaemonRunningAsync())
        {
            return true;
        }

        //if (AndroidPermissionService.GetIgnoreBatteryOptimizationsEnabled())
        //{
        //    var activity = Platform.CurrentActivity;
        //    if (activity is null)
        //        return false;

        //    var powerManager = (PowerManager?)activity.ApplicationContext?.GetSystemService(Context.PowerService);
        //    _wakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial, "HavenoDaemon:WakeLock");
        //    _wakeLock?.Acquire();
        //}

        //var startBackendIntent = new Intent(Platform.AppContext, typeof(BackendService))
        //                .SetAction("ACTION_START_BACKEND")
        //                .PutExtra("password", password)
        //                .PutExtra("host", host);

        //ContextCompat.StartForegroundService(Platform.AppContext, startBackendIntent);

        await SecureStorageHelper.SetAsync("password", password);
        await SecureStorageHelper.SetAsync("host", host);
        _grpcChannelSingleton.CreateChannel(host, password);

        // TODO
        // When app uses optimized battery settings, these threads get killed.
        // I want these to just sleep when optimized so they resume when the app resumes

        _torReadyTCS = new();

        _ = Task.Run(() =>
        {
            _torCts = new();
            using var streamReader = Proot.RunProotUbuntuCommand("tor", _torCts.Token);

            string? line;
            while ((line = streamReader.ReadLine()) != null)
            {
                Console.WriteLine(line);

                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] == '%')
                    {
                        StringBuilder stringBuilder = new();
                        for (int j = i - 1; j > 0; j--)
                        {
                            if (line[j] > '9' || line[j] < '0')
                                break;

                            stringBuilder.Append(line[j]);
                        }

                        if (stringBuilder.Length > 0)
                        {
                            var percentage = new string(stringBuilder.ToString().Reverse().ToArray());

                            progressCb?.Invoke($"Tor bootstrapping: {percentage}%");

                            if (percentage == "100")
                            {
                                _torReadyTCS.SetResult();
                            }
                        }
                    }
                }
            }
        });

        _ = Task.Run(async () =>
        {
            await _torReadyTCS.Task;

            progressCb?.Invoke("Starting daemon");

            _daemonCts = new();
            using var streamReader = Proot.RunProotUbuntuCommand("bash", _daemonCts.Token, "-c", $"/usr/share/haveno/haveno-daemon --baseCurrencyNetwork=XMR_STAGENET --useLocalhostForP2P=false --useDevPrivilegeKeys=false --nodePort=9999 --appName=haveno-XMR_STAGENET_user1 --apiPassword={password} --apiPort=3201 --passwordRequired=false --useNativeXmrWallet=false --torControlHost=127.0.0.1 --torControlPort=9051");

            string? line;
            while ((line = streamReader.ReadLine()) != null)
            {
                Console.WriteLine(line);

                if (line.Contains("Init wallet"))
                {
                    progressCb?.Invoke("Initializing wallet");
                }
                else if (line.Contains("walletInitialized=true"))
                {

                }
            }
        });

        return true;
    }

    public override Task<bool> TryStartTorAsync()
    {
        throw new NotImplementedException();
    }

    public override async Task StopHavenoDaemonAsync()
    {
        if (_daemonCts is not null)
            await _daemonCts.CancelAsync();

        if (_torCts is not null)
            await _torCts.CancelAsync();
    }
}
