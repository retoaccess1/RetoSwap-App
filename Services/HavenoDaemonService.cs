using Grpc.Core;
using HavenoSharp.Services;
using Manta.Helpers;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Manta.Services;

//public enum HavenoInstallationStatus
//{
//    None,
//    NotInstalled,
//    InstalledLatest,
//    InstalledOutOfDate
//}

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

    protected async Task<string> GetHavenoLatestVersionAsync()
    {
        var selectedRepo = _havenoRepos[0];

        using var client = new HttpClient();
        var response = await client.GetAsync($"https://github.com/{selectedRepo}/releases/latest");

        var latestVersion = response.RequestMessage?.RequestUri?.ToString().Split("tag/v").ElementAt(1);
        if (latestVersion is null)
            throw new Exception("Could not parse latest version");  // Bad if this fails, could get stuck - fallback to older known version or should we just not parse as versions

        return latestVersion;
    }

    protected async Task<string?> GetHavenoLocalVersionAsync()
    {
        if (Directory.GetFiles(_daemonPath).Contains("version"))
            return null; // Not installed

        using var fileStream = File.Open(Path.Combine(_daemonPath, "version"), FileMode.OpenOrCreate, FileAccess.ReadWrite);
        using var reader = new StreamReader(fileStream);

        var currentHavenoVersion = await reader.ReadToEndAsync();
        reader.Close();

        return currentHavenoVersion;
    }

    public virtual async Task TryUpdateHavenoAsync(IProgress<double> progressCb)
    {
        var currentHavenoVersion = await GetHavenoLocalVersionAsync();
        if (string.IsNullOrEmpty(currentHavenoVersion))
            return;

        var latestVersion = await GetHavenoLatestVersionAsync();

        // Do we even need to check that the version is greater? Already fetching the latest version
        // Would be more robust if convention changes
        if (Version.Parse(latestVersion) > Version.Parse(currentHavenoVersion))
        {
            var shouldUpdate = await Application.Current!.MainPage!.DisplayAlert("Update available", $"There is a new version available: v{latestVersion}. Current version is: v{currentHavenoVersion}.\nWould you like to update?", "Yes", "No");
            if (shouldUpdate)
            {
                var selectedRepo = _havenoRepos[0];
                await FetchHavenoDaemonAsync(selectedRepo, latestVersion, progressCb);
            }
        }
    }

    protected async Task FetchHavenoDaemonAsync(string selectedRepo, string version, IProgress<double> progressCb)
    {
        progressCb.Report(102f);

        using var client = new HttpClient();
        using var stream = await HttpClientHelper.DownloadWithProgressAsync($"https://github.com/{selectedRepo}/releases/download/v{version}/{_os}.zip", progressCb, client);

        if (Directory.Exists(_daemonPath))
            Directory.Delete(_daemonPath, true);

        ZipFile.ExtractToDirectory(stream, _daemonPath);

        using var fileStream = File.Open(Path.Combine(_daemonPath, "version"), FileMode.OpenOrCreate, FileAccess.ReadWrite);
        using var writer = new StreamWriter(fileStream);
        writer.Write(version);
        writer.Close();
    }

    // First time setup
    protected async Task DownloadHavenoDaemonAsync(IProgress<double> progressCb)
    {
        var selectedRepo = _havenoRepos[0];
        var latestVersion = await GetHavenoLatestVersionAsync();

        await FetchHavenoDaemonAsync(selectedRepo, latestVersion, progressCb);
    }

    public async Task<bool> IsHavenoDaemonRunningAsync(CancellationToken cancellationToken = default)
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
