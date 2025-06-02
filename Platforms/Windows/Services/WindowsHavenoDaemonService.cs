using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Helpers;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Manta.Services;

public enum HavenoInstallationStatus
{
    None,
    NotInstalled,
    InstalledLatest,
    InstalledOutOfDate
}

public class WindowsHavenoDaemonService : HavenoDaemonServiceBase
{
    private string[] _havenoRepos = ["atsamd21/haveno", "haveno-dex/haveno"];
    private string? _currentHavenoVersion;
    private string _os;
    private HavenoInstallationStatus _havenoInstallationStatus;

    private readonly GrpcChannelSingleton _grpcChannelSingleton;

    public WindowsHavenoDaemonService(GrpcChannelSingleton grpcChannelSingleton,
        IServiceProvider serviceProvider,
        IHavenoWalletService walletService,
        IHavenoVersionService versionService,
        IHavenoAccountService accountService
        ) : base(serviceProvider, walletService, versionService, accountService)
    {
        _grpcChannelSingleton = grpcChannelSingleton;
        _os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux-x86_64";
    }

    public async Task FetchHavenoDaemon(string daemonPath, string selectedRepo, string latestVersion)
    {
        using var client = new HttpClient();

        var bytes = await client.GetByteArrayAsync($"https://github.com/{selectedRepo}/releases/download/v{latestVersion}/{_os}.zip");

        using MemoryStream memoryStream = new(bytes);

        ZipFile.ExtractToDirectory(memoryStream, daemonPath);

        using var fileStream = File.Open(Path.Combine(daemonPath, "version.txt"), FileMode.OpenOrCreate, FileAccess.ReadWrite);
        using var writer = new StreamWriter(fileStream);
        writer.Write(latestVersion);
        writer.Close();
    }

    public override async Task InstallHavenoDaemonAsync()
    {
        Console.WriteLine("Initializing...");

        var daemonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Daemon");
        Directory.CreateDirectory(daemonPath);

        var directories = Directory.GetDirectories(daemonPath);
        if (directories.Length == 0)
        {
            _havenoInstallationStatus = HavenoInstallationStatus.NotInstalled;
        }
        else
        {
            var havenoDirectory = directories.Where(x => x.Contains(_os, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
            if (havenoDirectory is not null)
            {
                using var fileStream = File.Open(Path.Combine(daemonPath, "version.txt"), FileMode.OpenOrCreate, FileAccess.ReadWrite);
                using var reader = new StreamReader(fileStream);

                _currentHavenoVersion = await reader.ReadToEndAsync();
                reader.Close();
            }
            else
            {
                // Daemon directory not empty but does not contain a haveno directory?
                _havenoInstallationStatus = HavenoInstallationStatus.NotInstalled;
            }
        }

        var selectedRepo = _havenoRepos[0];

        // Could also get a list of all releases and let user choose
        using var client = new HttpClient();
        var response = await client.GetAsync($"https://github.com/{selectedRepo}/releases/latest");

        // Are all prepended with "v"?
        var latestVersion = response.RequestMessage?.RequestUri?.ToString().Split("tag/v").ElementAt(1);
        if (latestVersion is null)
            throw new Exception("Could not parse latest version");

        if (_havenoInstallationStatus == HavenoInstallationStatus.NotInstalled)
        {
            Console.WriteLine("Haveno daemon not installed, will install now");

            await FetchHavenoDaemon(daemonPath, selectedRepo, latestVersion);

            Console.WriteLine("Haveno daemon finished installing");
        }
        else
        {
            if (string.IsNullOrEmpty(_currentHavenoVersion))
                throw new Exception("_currentHavenoVersion was null");

            if (Version.Parse(latestVersion) > Version.Parse(_currentHavenoVersion))
            {
                _havenoInstallationStatus = HavenoInstallationStatus.InstalledOutOfDate;

                Console.WriteLine($"There is a new version available: v{latestVersion}. Current version is: v{_currentHavenoVersion}. Would you like to update? Enter [y] or [yes], default is no");

                var shouldUpdate = true;
                if (shouldUpdate)
                {
                    Console.WriteLine("Updating Haveno daemon...");

                    // Data is saved in appdata/user folders so this is fine
                    Directory.Delete(daemonPath, true);

                    await FetchHavenoDaemon(daemonPath, selectedRepo, latestVersion);
                }
            }
            else
            {
                _havenoInstallationStatus = HavenoInstallationStatus.InstalledLatest;
                Console.WriteLine("Haveno daemon up to date");
            }
        }
    }

    public override async Task<bool> GetIsDaemonInstalledAsync()
    {
        var daemonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Daemon");

        var directories = Directory.GetDirectories(daemonPath);
        if (directories.Length == 0)
        {
            return false;
        }
        else
        {
            var havenoDirectory = directories.Where(x => x.Contains(_os, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
            if (havenoDirectory is not null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public override async Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host, Action<string>? progressCb = default)
    {
        var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        if (string.IsNullOrEmpty(currentDirectory))
            throw new Exception();

        var procesess = Process.GetProcesses();
        int i = 0;
        foreach (var p in procesess)
        {
            if (p.ProcessName.CompareTo("Manta") == 0)
            {
                i++;
            }
        }

        if (i > 1)
        {
            return true;
        }

        await SecureStorageHelper.SetAsync("password", password);
        await SecureStorageHelper.SetAsync("host", host);

        _grpcChannelSingleton.CreateChannel(host, password);

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        var daemonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Daemon");

        ProcessStartInfo startInfo = new()
        {
            FileName = isWindows ? Path.Combine(daemonPath, _os, "haveno-daemon.bat") : Path.Combine(daemonPath, _os, "haveno-daemon"),
            Arguments = "--baseCurrencyNetwork=XMR_STAGENET " +
                        "--useLocalhostForP2P=false " +
                        "--useDevPrivilegeKeys=false " +
                        "--nodePort=9999 " +
                        "--appName=haveno-XMR_STAGENET_user1 " +
                        $"--apiPassword={password} " +
                        "--apiPort=3201 " +
                        "--passwordRequired=false " +
                        "--useNativeXmrWallet=false",

            WorkingDirectory = currentDirectory
        };

        var process = Process.Start(startInfo);

        if (process is null)
            throw new Exception("process was null");

        process.Exited += (sender, e) =>
        {
            Console.WriteLine("Haveno daemon exited");
        };

        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("SIGINT received");

            process.StandardInput.Close();

            e.Cancel = false;
        };

        //await process.WaitForExitAsync();

        return true;
    }

    public override Task<bool> TryStartTorAsync()
    {
        throw new NotImplementedException();
    }

    public override Task StopHavenoDaemonAsync()
    {
        throw new NotImplementedException();
    }
}
