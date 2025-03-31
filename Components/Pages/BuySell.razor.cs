using Blazored.LocalStorage;
using Haveno.Proto.Grpc;
using Manta.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

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

    public CancellationTokenSource CancellationTokenSource { get; set; } = new();

    public List<OfferInfo> Offers { get; set; } = [];

    public string SelectedCurrencyCode 
    { 
        get; 
        set 
        { 
            field = value; 
            Console.WriteLine(value);

            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new();

            OfferFetchTask = FetchOffersAsync();
        } 
    } = string.Empty;

    public string SelectedPaymentMethod
    {
        get;
        set
        {
            field = value;
            Console.WriteLine(value);

            // This is stupid!
            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new();

            OfferFetchTask = FetchOffersAsync();
        }
    } = string.Empty;

    // Needs to be fetched from the users preferences
    public Dictionary<string, string> CurrencyCodes { get; set; } = Enum.GetNames(typeof(Helpers.Currency)).ToDictionary(x => x, x => x);
    public Dictionary<string, string> PaymentMethods { get; set; } = [];

    public bool IsCreatingOffer { get; set; }
    public int OfferPaymentType { get; set; }
    public string Direction { get; set; } = "BUY";

    public Task? OfferFetchTask;

    public bool IsToggled
    {
        get; 
        set
        {
            field = value;
            Direction = value ? "SELL" : "BUY";

            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new();

            OfferFetchTask = FetchOffersAsync();
        }
    }

    public void CloseCreateOffer()
    {
        IsCreatingOffer = false;
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
                OfferFetchTask = FetchOffersAsync();

                using var paymentAccountChannel = new GrpcChannelHelper();
                var paymentAccountsClient = new PaymentAccountsClient(paymentAccountChannel.Channel);

                var paymentMethodsResponse = await paymentAccountsClient.GetPaymentMethodsAsync(new GetPaymentMethodsRequest());
                PaymentMethods = paymentMethodsResponse.PaymentMethods.ToDictionary(x => x.Id, x => x.Id);

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
                    Offers = [.. offers.Offers];
                }

                StateHasChanged();
                Console.WriteLine($"Fetched {Offers.Count} offers");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            await Task.Delay(5_000, CancellationTokenSource.Token);
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