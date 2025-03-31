using Haveno.Proto.Grpc;
using Manta.Helpers;
using Microsoft.AspNetCore.Components;
using Protobuf;

using static Haveno.Proto.Grpc.Offers;
using static Haveno.Proto.Grpc.PaymentAccounts;

namespace Manta.Components.Pages;

public partial class CreateOffer : ComponentBase
{
    [Parameter]
    public string Direction { get; set; } = string.Empty;
    [Parameter]
    public EventCallback OnCloseCreateOffer { get; set; }

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
        }
    } = string.Empty;

    private ulong _piconeroAmount;
    public decimal Amount
    {
        get;
        set
        {
            field = value;
            _piconeroAmount = value.ToPiconero();

            if (_piconeroMinimumAmount == 0)
            {
                MinimumAmount = value;
            }
        }
    }
    private ulong _piconeroMinimumAmount;
    public decimal MinimumAmount
    {
        get;
        set
        {
            field = value;
            _piconeroMinimumAmount = value.ToPiconero();

            if (_piconeroMinimumAmount > _piconeroAmount)
                Amount = value;
        }
    }

    public decimal MarketPriceMarginPct { get; set; }
    public decimal FiatAmount { get; set; }
    public decimal TriggerAmount { get; set; }

    public bool IsFetching { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
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

            var resonse = await offersClient.PostOfferAsync(new PostOfferRequest
            {
                Amount = _piconeroAmount,
                MinAmount = _piconeroMinimumAmount,
                PaymentAccountId = SelectedPaymentAccountId,
                CurrencyCode = SelectedCurrencyCode,
                Direction = Direction,
                TriggerPrice = TriggerAmount.ToString(),
                Price = FiatAmount.ToString(),
                SecurityDepositPct = 0.15,
                MarketPriceMarginPct = (double)MarketPriceMarginPct,
                //UseMarketBasedPrice = BelowMarketPercent == 0m,
            });

            await OnCloseCreateOffer.InvokeAsync();
        }
        catch
        {

        }

        IsFetching = false;
    }
}
