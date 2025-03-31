using Blazored.LocalStorage;
using Manta.Helpers;

namespace Manta.Services;

public class SetupService : ISetupService
{
    private readonly ILocalStorageService _localStorageService;

    public SetupService(ILocalStorageService localStorageService)
    {
        _localStorageService = localStorageService;
    }

    public async Task InitialSetupAsync()
    {
        var isSetup = await _localStorageService.GetItemAsync<bool>("isSetup");
        if (isSetup)
            return;

        var preferredCurrency = await _localStorageService.GetItemAsStringAsync("preferredCurrency");
        if (preferredCurrency is null)
        {
            await _localStorageService.SetItemAsStringAsync("preferredCurrency", Currency.GBP.ToString());
        }

        await _localStorageService.SetItemAsync("isSetup", true);
    }
}
