using Android.Content;
using AndroidX.Core.Content;
using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Helpers;
using System.Runtime.InteropServices;

namespace Manta.Services;

public class ProgressReceiver : BroadcastReceiver
{
    public event Action<string>? OnProgressChanged;
    public TaskCompletionSource CompletedTCS { get; } = new();

    public override void OnReceive(Context? context, Intent? intent)
    {
        var progress = intent?.GetStringExtra("progress");
        if (progress is null)
            return;

        OnProgressChanged?.Invoke(progress);

        var isDone = intent?.GetBooleanExtra("isDone", false);
        if (isDone is not null and true)
            CompletedTCS.SetResult();
    }
}

public class AndroidHavenoDaemonService : HavenoDaemonServiceBase
{
    private readonly GrpcChannelSingleton _grpcChannelSingleton;

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

    public override async Task InstallHavenoDaemonAsync(IProgress<double> progressCb)
    {
        using var ubuntuDownloadStream = await Proot.DownloadUbuntu(progressCb);
        await Proot.ExtractUbuntu(ubuntuDownloadStream, progressCb);

        var arch = RuntimeInformation.OSArchitecture.ToString() == "X64" ? "amd64" : "arm64";

        Proot.RunProotUbuntuCommand("rm", "/bin/java");
        Proot.RunProotUbuntuCommand("ln", "-s", $"/usr/lib/jvm/java-21-openjdk-{arch}/bin/java", "/bin/java");
        Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/java.security", $"/usr/lib/jvm/java-21-openjdk-{arch}/conf/security/java.security");
        Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/java.policy", $"/usr/lib/jvm/java-21-openjdk-{arch}/conf/security/java.policy");
        Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/default.policy", $"/usr/lib/jvm/java-21-openjdk-{arch}/lib/security/default.policy");
        Proot.RunProotUbuntuCommand("chmod", "+x", "/usr/share/haveno/haveno-daemon");
    }

    public override Task<bool> GetIsDaemonInstalledAsync()
    {
        try
        {
            var result = Proot.RunProotUbuntuCommand("echo", "check");
            if (!result.Contains("check"))
                return Task.FromResult(false);

            result = Proot.RunProotUbuntuCommand("java", "--version");
            if (!result.Contains("21"))
                return Task.FromResult(false);

            return Task.FromResult(true);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }

    public override async Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host, Action<string>? progressCb = default)
    {
        if (await IsHavenoDaemonRunningAsync())
        {
            return true;
        }

        await SecureStorageHelper.SetAsync("password", password);
        await SecureStorageHelper.SetAsync("host", host);

        _grpcChannelSingleton.CreateChannel(host, password);

        var receiver = new ProgressReceiver();
        receiver.OnProgressChanged += progressCb;

        var filter = new IntentFilter("com.companyname.manta.BACKEND_PROGRESS");

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            Platform.AppContext.RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
        }
        else
        {
            Platform.AppContext.RegisterReceiver(receiver, filter);
        }

        var startBackendIntent = new Intent(Platform.AppContext, typeof(BackendService))
                        .SetAction("ACTION_START_BACKEND")
                        .PutExtra("password", password);

        ContextCompat.StartForegroundService(Platform.AppContext, startBackendIntent);

        await receiver.CompletedTCS.Task;

        receiver.OnProgressChanged -= progressCb;
        Platform.AppContext.UnregisterReceiver(receiver);

        return true;
    }

    public override Task<bool> TryStartTorAsync()
    {
        throw new NotImplementedException();
    }

    public override Task StopHavenoDaemonAsync()
    {
        var startBackendIntent = new Intent(Platform.AppContext, typeof(BackendService))
                        .SetAction("ACTION_STOP_BACKEND");

        ContextCompat.StartForegroundService(Platform.AppContext, startBackendIntent);

        return Task.CompletedTask;
    }
}
