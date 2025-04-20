using Blazored.LocalStorage;
using Manta.Helpers;

namespace Manta.Services;

public class SetupService : ISetupService
{
    private readonly ILocalStorageService _localStorage;

    public SetupService(ILocalStorageService localStorageService)
    {
        _localStorage = localStorageService;
    }

    public async Task InitialSetupAsync()
    {
        var preferredCurrency = await _localStorage.GetItemAsStringAsync("preferredCurrency");
        if (preferredCurrency is null)
        {
            await _localStorage.SetItemAsStringAsync("preferredCurrency", Currency.GBP.ToString());
        }
    }
}
