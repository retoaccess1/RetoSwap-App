using Blazored.LocalStorage;
using Haveno.Proto.Grpc;
using Manta.Components.Reusable;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Globalization;

using static Haveno.Proto.Grpc.Offers;
using static Haveno.Proto.Grpc.PaymentAccounts;

namespace Manta.Components.Pages;

public partial class BuySell : ComponentBase, IDisposable
{
    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public IJSRuntime JS { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;

    public CancellationTokenSource CancellationTokenSource { get; set; } = new();

    public List<OfferInfo> Offers { get; set; } = [];

    public bool IsFetching { get; set; }

    public string SelectedCurrencyCode 
    { 
        get; 
        set 
        { 
            field = value;
            ResetFetch();
        } 
    } = string.Empty;

    public string SelectedPaymentMethod
    {
        get;
        set
        {
            field = value;
            ResetFetch();
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

            ResetFetch();
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

    public bool IsCollapsed { get; set; }

    public void CloseCreateOffer()
    {
        IsCreatingOffer = false;
    }

    public SemaphoreSlim ResetSemaphore { get; set; } = new(1);

    public void ResetFetch()
    {
        if (ResetSemaphore.CurrentCount == 0)
            return;

        ResetSemaphore.Wait();

        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();
        CancellationTokenSource = new();

        Task.Run(async() => {
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
                using var paymentAccountChannel = new GrpcChannelHelper();
                var paymentAccountsClient = new PaymentAccountsClient(paymentAccountChannel.Channel);

                var paymentMethodsResponse = await paymentAccountsClient.GetPaymentMethodsAsync(new GetPaymentMethodsRequest());

                var filteredPaymentMethodIds = paymentMethodsResponse.PaymentMethods
                    .Select(x => x.Id);

                TraditionalPaymentMethods = PaymentMethodsHelper.PaymentMethodsDictionary
                    .Where(x => filteredPaymentMethodIds.Contains(x.Key)).ToDictionary();

                var cryptoPaymentMethodsResponse = await paymentAccountsClient.GetCryptoCurrencyPaymentMethodsAsync(new GetCryptoCurrencyPaymentMethodsRequest());

                var filteredCryptoPaymentMethodIds = cryptoPaymentMethodsResponse.PaymentMethods
                    .Select(x => x.Id);

                CryptoPaymentMethods = PaymentMethodsHelper.PaymentMethodsDictionary
                    .Where(x => filteredCryptoPaymentMethodIds.Contains(x.Key)).ToDictionary();

                PreferredCurrency = await LocalStorage.GetItemAsStringAsync("preferredCurrency") ?? "USD";
                PreferredCurrencyFormat = CurrencyCultureInfo.GetFormatForCurrency((Currency)Enum.Parse(typeof(Currency), PreferredCurrency));

                CurrentMarketPrice = BalanceSingleton.MarketPriceInfoDictionary[PreferredCurrency].ToString("0.00");

                VisiblePaymentMethods = TraditionalPaymentMethods;
                VisibleCurrencyCodes = TraditionalCurrencyCodes;

                OfferFetchTask = FetchOffersAsync();
                //OfferFetchTask = Task.Run(FetchOffersAsync);

                break;
            }
            catch
            {

            }

            await Task.Delay(5_000);
        }

        await base.OnInitializedAsync();
    }

    private async Task FetchOffersAsync()
    {
        while (true)
        {
            try
            {
                IsFetching = true;

                using var grpcChannelHelper = new GrpcChannelHelper();
                var offersClient = new OffersClient(grpcChannelHelper.Channel);

                var offersRequest = new GetOffersRequest
                {
                    CurrencyCode = SelectedCurrencyCode,
                    Direction = Direction == "BUY" ? "SELL" : "BUY"
                };

                var offers = await offersClient.GetOffersAsync(offersRequest, cancellationToken: CancellationTokenSource.Token);
                if (!string.IsNullOrEmpty(SelectedPaymentMethod))
                {
                    Offers = [.. offers.Offers.Where(x => x.PaymentMethodId == SelectedPaymentMethod)];
                }
                else
                {
                    switch (OfferPaymentType)
                    {
                        case 0:
                            Offers = [.. offers.Offers.Where(x => TraditionalPaymentMethods.ContainsKey(x.PaymentMethodId)).Where(x => x.PaymentMethodId != "BLOCK_CHAINS")];
                            break;
                        case 1:
                            Offers = [.. offers.Offers.Where(x => CryptoPaymentMethods.ContainsKey(x.PaymentMethodId))];
                            break;
                        case 2:
                            Offers = [.. offers.Offers.Where(x => !TraditionalPaymentMethods.ContainsKey(x.PaymentMethodId)).Where(x => x.PaymentMethodId != "BLOCK_CHAINS")];
                            break;
                        default: 
                            Offers = [.. offers.Offers];
                            break;
                    }
                }

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
        NavigationManager.NavigateTo("/myoffers?title=My%20Offers");
    }

    public void Dispose()
    {
        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();
    }
}