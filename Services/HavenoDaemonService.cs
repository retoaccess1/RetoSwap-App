namespace Manta.Services;

public interface IHavenoDaemonService
{
    Task<bool> GetIsDaemonInstalledAsync();
    Task InstallHavenoDaemon();
    Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host);
}
