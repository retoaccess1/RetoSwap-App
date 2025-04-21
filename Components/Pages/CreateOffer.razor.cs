using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Protobuf;

using static Haveno.Proto.Grpc.Offers;
using static Haveno.Proto.Grpc.PaymentAccounts;

namespace Manta.Components.Pages;

public class AdjustedPrices
{
    public decimal? FiatPrice;
    public decimal MoneroAmount;
}

public partial class CreateOffer : ComponentBase
{
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;

    [Parameter]
    public string Direction { get; set; } = string.Empty;
    [Parameter]
    public EventCallback OnCloseCreateOffer { get; set; }
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    // Repeat
    public Dictionary<string, string> CurrencyCodes { get; set; } = [];
    public string SelectedPaymentAccountId 
    { 
        get; 
        set 
        { 
            field = value;
            CurrencyCodes = ProtoPaymentAccounts.FirstOrDefault(x => x.Id == value)?.TradeCurrencies.ToDictionary(x => x.Code, x => $"{x.Name} ({x.Code})") ?? [];
            SelectedCurrencyCode = ProtoPaymentAccounts.FirstOrDefault(x => x.Id == SelectedPaymentAccountId)?.TradeCurrencies.FirstOrDefault()?.Code ?? throw new Exception();

            Clear();

            StateHasChanged();
        } 
    } = string.Empty;

    public List<PaymentAccount> ProtoPaymentAccounts { get; set; } = [];
    public Dictionary<string, string> PaymentAccounts { get; set; } = [];

    public string SelectedCurrencyCode
    {
        get;
        set
        {
            field = value;
            Clear();
        }
    } = string.Empty;

    // Currently does not respect trade limits, TODO
    private ulong _piconeroAmount;
    public decimal MoneroAmount
    {
        get;
        set
        {
            var adjustedPrices = GetAdjustedPrices(value);
            if (adjustedPrices is null)
            {
                field = value;
            }
            else
            {
                field = adjustedPrices.MoneroAmount;
                FiatPrice = adjustedPrices.FiatPrice;
            }

            if (field < MinimumMoneroAmount || MinimumMoneroAmount == 0m)
            {
                MinimumMoneroAmount = field;
            }

            _piconeroAmount = field.ToPiconero();
        }
    }

    private ulong _minimumPiconeroAmount;
    public decimal MinimumMoneroAmount
    {
        get;
        set
        {
            var adjustedPrices = GetAdjustedPrices(value);
            if (adjustedPrices is null)
            {
                field = value;
            }
            else
            {
                field = adjustedPrices.MoneroAmount;
            }

            if (field > MoneroAmount)
            {
                MoneroAmount = field;
            }

            _minimumPiconeroAmount = field.ToPiconero();
        }
    }

    public decimal? MarketPriceMarginPct 
    { 
        get;
        set
        {
            if (value >= 100m)
                field = 99.99m;
            else
                field = value;

            MoneroAmount = MoneroAmount;
            MinimumMoneroAmount = MinimumMoneroAmount;
        }
    }

    // When set from input, should round and adjust monero amount TODO
    // When set externally null marketpricemarginpct
    public decimal? FiatPrice 
    {
        get;
        set
        {
            if (value is null)
                field = null;
            else
            {
                field = Math.Round(value.Value);

                //if (BalanceSingleton.MarketPriceInfoDictionary.TryGetValue(SelectedCurrencyCode, out var price))
                //{
                //    MoneroAmount = Math.Round(MoneroAmount / price, 4);
                //    MinimumMoneroAmount = MoneroAmount;
                //}
            }
        }
    }

    public decimal TriggerAmount { get; set; }
    public decimal SecurityDepositPct { get; set; } = 15m;

    public bool IsFetching { get; set; }

    public void Clear()
    {
        MinimumMoneroAmount = 0;
        MoneroAmount = 0;
        FiatPrice = null;
        MarketPriceMarginPct = null;
        TriggerAmount = 0;
        SecurityDepositPct = 15m;
    }

    // Gives the adjusted amounts so that the fiat value is a whole number
    public AdjustedPrices? GetAdjustedPrices(decimal unadjustedMoneroAmount)
    {
        AdjustedPrices? adjustedPrices = null;

        if (MarketPriceMarginPct is not null && BalanceSingleton.MarketPriceInfoDictionary.TryGetValue(SelectedCurrencyCode, out var price))
        {
            decimal adjustedMktPrice;
            var percent = (MarketPriceMarginPct.Value / 100m);

            if (Direction == "BUY")
            {
                adjustedMktPrice = price - (price * percent);
            }
            else
            {
                adjustedMktPrice = price + (price * percent);
            }

            adjustedPrices = new AdjustedPrices
            {
                FiatPrice = Math.Round(adjustedMktPrice * unadjustedMoneroAmount),
            };

            adjustedPrices.MoneroAmount = Math.Round(adjustedPrices.FiatPrice.Value / adjustedMktPrice, 4);
        }

        return adjustedPrices;
    }

    protected override async Task OnInitializedAsync()
    {
        using var grpcChannelHelper = new GrpcChannelHelper();
        var paymentAccountsClient = new PaymentAccountsClient(grpcChannelHelper.Channel);

        var paymentAccountResponse = await paymentAccountsClient.GetPaymentAccountsAsync(new GetPaymentAccountsRequest());
        PaymentAccounts = paymentAccountResponse.PaymentAccounts.ToDictionary(x => x.Id, x => x.AccountName);
        ProtoPaymentAccounts = paymentAccountResponse.PaymentAccounts.ToList();
        
        if (ProtoPaymentAccounts.Count != 0)
        {
            // Get trade limits
            //paymentAccountResponse.PaymentAccounts.FirstOrDefault(x => x.Id == SelectedPaymentAccountId).PaymentMethod.

            SelectedPaymentAccountId = PaymentAccounts.Select(x => x.Key).FirstOrDefault() ?? string.Empty;
            SelectedCurrencyCode = paymentAccountResponse.PaymentAccounts.FirstOrDefault(x => x.Id == SelectedPaymentAccountId)?.TradeCurrencies.FirstOrDefault()?.Code ?? throw new Exception();
        }

        await base.OnInitializedAsync();
    }

    public async Task PostOfferAsync()
    {
        try
        {
            IsFetching = true;

            using var channelHelper = new GrpcChannelHelper();
            var offersClient = new OffersClient(channelHelper.Channel);

            var request = new PostOfferRequest
            {
                Amount = _piconeroAmount,
                MinAmount = _minimumPiconeroAmount,
                PaymentAccountId = SelectedPaymentAccountId,
                CurrencyCode = SelectedCurrencyCode,
                Direction = Direction,
                SecurityDepositPct = (double)(SecurityDepositPct / 100),

                TriggerPrice = TriggerAmount.ToString(),
                MarketPriceMarginPct = 0,
                Price = "",
                UseMarketBasedPrice = true
            };

            if (MarketPriceMarginPct is null)
            {
                request.MarketPriceMarginPct = 0;
            }
            else 
            {
                //request.UseMarketBasedPrice = false;
                //request.MarketPriceMarginPct = (double)(MarketPriceMarginPct * 100);
                request.MarketPriceMarginPct = (double)(MarketPriceMarginPct);
            }

            var response = await offersClient.PostOfferAsync(request);

            NavigationManager.NavigateTo("/myoffers?title=My%20Offers");

            //await OnCloseCreateOffer.InvokeAsync();
        }
        catch
        {
            throw;
        }
        finally
        {
            IsFetching = false;
        }
    }
}
