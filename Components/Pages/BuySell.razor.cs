using Blazored.LocalStorage;
using HavenoSharp.Models;
using HavenoSharp.Services;
using Manta.Components.Reusable;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Globalization;

namespace Manta.Components.Pages;

public partial class BuySell : ComponentBase, IDisposable
{
    [Inject]
    public IHavenoPaymentAccountService PaymentAccountService { get; set; } = default!;
    [Inject]
    public IHavenoOfferService OfferService { get; set; } = default!;
    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public IJSRuntime JS { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;

    public CancellationTokenSource CancellationTokenSource { get; set; } = new();

    public List<OfferInfo> FilteredOffers { get { return FilterOffers(Offers); } }
    public List<OfferInfo> Offers { get; set; } = [];

    public bool IsFetching { get; set; }

    // Does not update when these change
    public string SelectedCurrencyCode
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                ResetFetch();
            }
        }
    } = string.Empty;

    public string SelectedPaymentMethod
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                ResetFetch();
            }
        }
    } = string.Empty;

    // Needs to be fetched from the users preferences
    public Dictionary<string, string> TraditionalCurrencyCodes { get; set; } = CurrencyCultureInfo.GetCurrencyFullNamesAndCurrencyCodeDictionary().ToDictionary();
    public Dictionary<string, string> CryptoCurrencyCodes { get; set; } = CryptoCurrencyHelper.CryptoCurrenciesDictionary;
    public Dictionary<string, string> VisibleCurrencyCodes { get; set; } = [];
    public Dictionary<string, string> TraditionalPaymentMethods { get; set; } = [];
    public Dictionary<string, string> CryptoPaymentMethods { get; set; } = [];
    public Dictionary<string, string> VisiblePaymentMethods { get; set; } = [];

    public bool IsCreatingOffer { get; set; }
    public int OfferPaymentType 
    { 
        get; 
        set 
        {
            field = value;
            switch (field)
            {
                case 0:
                    VisiblePaymentMethods = TraditionalPaymentMethods;
                    VisibleCurrencyCodes = TraditionalCurrencyCodes;
                    break;
                case 1:
                    VisiblePaymentMethods = CryptoPaymentMethods;
                    VisibleCurrencyCodes = CryptoCurrencyCodes;
                    break;
                case 2:
                    break;
                default: break;
            }

            // Revisit, is this wanted?
            CurrencySearchableDropdown.Clear();
            PaymentMethodSearchableDropdown.Clear();
            SelectedCurrencyCode = string.Empty;
            SelectedPaymentMethod = string.Empty;
        } 
    }
    public string Direction { get; set; } = "BUY";

    public Task? OfferFetchTask;

    public bool IsToggled
    {
        get; 
        set
        {
            field = value;
            Direction = value ? "SELL" : "BUY";
            ResetFetch();
        }
    }

    public string PreferredCurrency { get; set; } = string.Empty;
    public string CurrentMarketPrice { get; set; } = string.Empty;
    public NumberFormatInfo PreferredCurrencyFormat { get; set; } = default!;

    public SearchableDropdown CurrencySearchableDropdown { get; set; } = default!;
    public SearchableDropdown PaymentMethodSearchableDropdown { get; set; } = default!;

    public bool IsCollapsed { get; set; } = true;

    public void CloseCreateOffer()
    {
        IsCreatingOffer = false;
    }

    public SemaphoreSlim ResetSemaphore { get; set; } = new(1);
    public void ResetFetch()
    {
        if (!ResetSemaphore.Wait(0))
            return;

        Task.Run(async() => {
            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new();

            if (OfferFetchTask is not null)
                await OfferFetchTask;

            OfferFetchTask = FetchOffersAsync();

            ResetSemaphore.Release();
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        while (true)
        {
            try
            {
                var paymentMethods = await PaymentAccountService.GetPaymentMethodsAsync();
                var filteredPaymentMethodIds = paymentMethods
                    .Select(x => x.Id);

                TraditionalPaymentMethods = PaymentMethodsHelper.PaymentMethodsDictionary
                    .Where(x => filteredPaymentMethodIds.Contains(x.Key)).ToDictionary();

                var cryptoPaymentMethods= await PaymentAccountService.GetCryptoCurrencyPaymentMethodsAsync();

                var filteredCryptoPaymentMethodIds = cryptoPaymentMethods
                    .Select(x => x.Id);

                CryptoPaymentMethods = PaymentMethodsHelper.PaymentMethodsDictionary
                    .Where(x => filteredCryptoPaymentMethodIds.Contains(x.Key)).ToDictionary();

                PreferredCurrency = await LocalStorage.GetItemAsStringAsync("preferredCurrency") ?? CurrencyCultureInfo.FallbackCurrency;
                PreferredCurrencyFormat = CurrencyCultureInfo.GetFormatForCurrency((Currency)Enum.Parse(typeof(Currency), PreferredCurrency))!;

                try
                {
                    // If there is no price data for this currency then fallback to USD
                    CurrentMarketPrice = BalanceSingleton.MarketPriceInfoDictionary[PreferredCurrency].ToString("0.00");
                }
                catch (KeyNotFoundException)
                {
                    CurrentMarketPrice = BalanceSingleton.MarketPriceInfoDictionary[CurrencyCultureInfo.FallbackCurrency].ToString("0.00");
                    PreferredCurrency = CurrencyCultureInfo.FallbackCurrency;
                    PreferredCurrencyFormat = CurrencyCultureInfo.GetFormatForCurrency((Currency)Enum.Parse(typeof(Currency), PreferredCurrency))!;
                }

                VisiblePaymentMethods = TraditionalPaymentMethods;
                VisibleCurrencyCodes = TraditionalCurrencyCodes;

                OfferFetchTask = Task.Run(FetchOffersAsync);

                break;
            }
            catch
            {

            }

            await Task.Delay(5_000);
        }

        await base.OnInitializedAsync();
    }

    private List<OfferInfo> FilterOffers(List<OfferInfo> offers)
    {
        if (!string.IsNullOrEmpty(SelectedPaymentMethod))
        {
            return [.. offers.Where(x => x.PaymentMethodId == SelectedPaymentMethod)];
        }
        else
        {
            switch (OfferPaymentType)
            {
                case 0:
                    return [.. offers.Where(x => TraditionalPaymentMethods.ContainsKey(x.PaymentMethodId)).Where(x => x.PaymentMethodId != "BLOCK_CHAINS")];
                case 1:
                    return [.. offers.Where(x => CryptoPaymentMethods.ContainsKey(x.PaymentMethodId))];
                case 2:
                    return [.. offers.Where(x => !TraditionalPaymentMethods.ContainsKey(x.PaymentMethodId)).Where(x => x.PaymentMethodId != "BLOCK_CHAINS")];
                default:
                    return [.. offers];
            }
        }
    }

    private async Task FetchOffersAsync()
    {
        while (true)
        {
            try
            {
                IsFetching = true;

                Offers = await OfferService.GetOffersAsync(SelectedCurrencyCode, Direction == "BUY" ? "SELL" : "BUY");

                IsFetching = false;
                await InvokeAsync(StateHasChanged);
                Console.WriteLine($"Fetched {Offers.Count} offers");

                await Task.Delay(5_000, CancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                try
                {
                    await Task.Delay(5_000, CancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    public void NavigateToMyOffers()
    {
        NavigationManager.NavigateTo("buysell/myoffers?title=My%20Offers");
    }

    public void Dispose()
    {
        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();
    }
}