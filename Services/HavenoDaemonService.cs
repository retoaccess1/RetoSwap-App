using Grpc.Core;
using HavenoSharp.Services;
using Manta.Helpers;
using System.Runtime.InteropServices;

namespace Manta.Services;

public interface IHavenoDaemonService
{
    Task<bool> GetIsDaemonInstalledAsync();
    Task InstallHavenoDaemonAsync(IProgress<double> progressCb);
    Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host, Action<string>? progressCb = default);
    Task<bool> TryStartTorAsync();
    Task<bool> WaitHavenoDaemonInitializedAsync(CancellationToken cancellationToken = default);
    Task<bool> WaitWalletInitializedAsync(CancellationToken cancellationToken = default);
    Task<bool> IsHavenoDaemonRunningAsync(CancellationToken cancellationToken = default);
    Task StopHavenoDaemonAsync();
    Task TryUpdateHavenoAsync(IProgress<double> progressCb);
    string GetDaemonPath();
}

public abstract class HavenoDaemonServiceBase : IHavenoDaemonService
{
    private readonly IHavenoVersionService _versionService;
    private readonly IHavenoAccountService _accountService;
    private readonly IHavenoWalletService _walletService;

    private readonly string[] _havenoRepos = ["atsamd21/haveno", "haveno-dex/haveno"];

    protected string _os;
    protected string _daemonPath;
    protected string _latestDaemonVersion = "1.1.2.5";

    public HavenoDaemonServiceBase(IHavenoWalletService walletService, IHavenoVersionService versionService, IHavenoAccountService accountService, string daemonPath)
    {
        _accountService = accountService;
        _walletService = walletService;
        _versionService = versionService;

        _os = RuntimeInformation.OSArchitecture.ToString() == "X64" ? "linux-x86_64" : "linux-aarch64";
        _daemonPath = daemonPath;
    }

    public abstract Task InstallHavenoDaemonAsync(IProgress<double> progressCb);
    public abstract Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host, Action<string>? progressCb = default);
    public abstract Task<bool> TryStartTorAsync();
    public abstract Task StopHavenoDaemonAsync();
    public abstract Task<bool> GetIsDaemonInstalledAsync();

    public string GetDaemonPath() => _daemonPath;

    protected async Task<string?> GetHavenoLocalVersionAsync()
    {
        return await SecureStorageHelper.GetAsync<string>("daemon-version");
    }

    public virtual async Task TryUpdateHavenoAsync(IProgress<double> progressCb)
    {
        var currentHavenoVersion = await GetHavenoLocalVersionAsync();
        if (string.IsNullOrEmpty(currentHavenoVersion) || currentHavenoVersion == _latestDaemonVersion)
            return;

        var selectedRepo = _havenoRepos[0];
        await FetchHavenoDaemonAsync(selectedRepo, _latestDaemonVersion, progressCb);
        await SecureStorageHelper.SetAsync("daemon-version", _latestDaemonVersion);
    }

    protected async Task FetchHavenoDaemonAsync(string selectedRepo, string version, IProgress<double> progressCb)
    {
        progressCb.Report(102f);

        using var client = new HttpClient();
        using var stream = await HttpClientHelper.DownloadWithProgressAsync($"https://github.com/{selectedRepo}/releases/download/v{version}/daemon-{_os}.jar", progressCb, client);

        if (Directory.Exists(_daemonPath))
            Directory.Delete(_daemonPath, true);
        else 
            Directory.CreateDirectory(_daemonPath);

        using var fileStream = File.Create(Path.Combine(_daemonPath, "daemon.jar"));
        await stream.CopyToAsync(fileStream);

        fileStream.Close();
    }

    // First time setup
    protected async Task DownloadHavenoDaemonAsync(IProgress<double> progressCb)
    {
        var selectedRepo = _havenoRepos[0];
        await FetchHavenoDaemonAsync(selectedRepo, _latestDaemonVersion, progressCb);
        await SecureStorageHelper.SetAsync("daemon-version", _latestDaemonVersion);
    }

    // Retry parameter?
    public async Task<bool> IsHavenoDaemonRunningAsync(CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < 5 && !cancellationToken.IsCancellationRequested; i++)
        {
            try
            {
                // Will tell us its running but not if its initialized
                await _versionService.GetVersionAsync(cancellationToken: cancellationToken);

                return true;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (RpcException)
            {
                // Might be running but wrong password?
            }
            catch (Exception)
            {

            }

            await Task.Delay(100, cancellationToken);
        }

        return false;
    }

    public async Task<bool> WaitHavenoDaemonInitializedAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var isAppInitialized = await _accountService.IsAppInitializedAsync(cancellationToken: cancellationToken);
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
                await Task.Delay(1_100, cancellationToken: cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    public async Task<bool> WaitWalletInitializedAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                await _walletService.GetBalancesAsync(cancellationToken);
                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch
            {

            }

            try
            {
                await Task.Delay(1_100, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }
    }
}
