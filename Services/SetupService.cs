using Blazored.LocalStorage;
using Manta.Helpers;

namespace Manta.Services;

public enum DaemonStatus
{
    NONE,
    RUNNING,
    NOT_INSTALLED,
    INSTALLED_COULD_NOT_START
}

public class SetupService : ISetupService
{
    private readonly ILocalStorageService _localStorage;

    public SetupService(ILocalStorageService localStorageService)
    {
        _localStorage = localStorageService;
    }

    public async Task InitialSetupAsync()
    {
        var isSetup = await _localStorage.GetItemAsync<bool>("isSetup");
        if (isSetup)
            return;

        var preferredCurrency = await _localStorage.GetItemAsStringAsync("preferredCurrency");
        if (preferredCurrency is null)
        {
            await _localStorage.SetItemAsStringAsync("preferredCurrency", Currency.GBP.ToString());
        }

        await _localStorage.SetItemAsync("isSetup", true);
    }

    // Should this try to start daemon?
    public async Task<DaemonStatus> GetDaemonStatusAsync()
    {
#if ANDROID

        if (!await TermuxSetupService.IsHavenoDaemonRunning())
        {
            // TODO ask system if Termux installed
            var isDaemonSetupCompleted = ((await _localStorage.GetItemAsync<bool>("termux-installed")) && (await _localStorage.GetItemAsync<bool>("termux-updated")));
            if (isDaemonSetupCompleted)
            {
                var successfullyStarted = await TermuxSetupService.TryStartHavenoDaemon();
                if (successfullyStarted) 
                {
                    return DaemonStatus.RUNNING;
                }

                return DaemonStatus.INSTALLED_COULD_NOT_START;
            }
            else
            {
                return DaemonStatus.NOT_INSTALLED;
            }
        }
        else
        {
            return DaemonStatus.RUNNING;
        }

#endif
        return DaemonStatus.NONE;
    }
}
