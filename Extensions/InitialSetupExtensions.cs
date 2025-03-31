using Blazored.LocalStorage;
using Manta.Helpers;

namespace Manta.Extensions;

public static class InitialSetupExtensions
{
    public static async Task<MauiApp> InitialSetupAsync(this MauiApp app)
    {
        using var scope = app.Services.CreateScope();
        var localStorage = scope.ServiceProvider.GetRequiredService<ILocalStorageService>();

        var isSetup = await localStorage.GetItemAsync<bool>("isSetup");
        if (isSetup)
            return app;

        var preferredCurrency = await localStorage.GetItemAsStringAsync("preferredCurrency");
        if (preferredCurrency is null)
        {
            await localStorage.SetItemAsStringAsync("preferredCurrency", CurrencyCultureInfo.GetCurrencyFullName(Currency.GBP));
        }

        await localStorage.SetItemAsync("isSetup", true);

        return app;
    }
}
