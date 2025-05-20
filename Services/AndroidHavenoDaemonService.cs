using Manta.Helpers;

namespace Manta.Services;

public class AndroidHavenoDaemonService : IHavenoDaemonService
{

    public AndroidHavenoDaemonService()
    {

    }

    public async Task InstallHavenoDaemon()
    {

    }

    public Task<bool> GetIsDaemonInstalledAsync()
    {
        return SecureStorageHelper.GetAsync<bool>("termux-installed");
    }

    public async Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host)
    {
        return true;
    }
}
