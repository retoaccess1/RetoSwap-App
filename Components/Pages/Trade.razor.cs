using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;

using static Haveno.Proto.Grpc.Trades;

namespace Manta.Components.Pages;

public partial class Trade : ComponentBase, IDisposable
{
    [Parameter]
    [SupplyParameterFromQuery]
    public string TradeId { get; set; } = string.Empty;
    public TradeInfo TradeInfo { get; set; } = default!;
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    public int SellerState { get; set; }
    public int BuyerState { get; set; }
    public bool IsBuyer { get; set; }

    public string[] BuyerSteps { get; set; } = ["Wait for blockchain confirmations", "Start payment", "Wait until payment arrived", "Completed"];
    public string[] SellerSteps { get; set; } = ["Wait for blockchain confirmations", "Wait until payment has been sent", "Confirm payment received", "Completed"];

    protected override async Task OnInitializedAsync()
    {
        while (true)
        {
            try
            {
                var tradeInfo = NotificationSingleton.TradeInfos[TradeId];
                if (tradeInfo is null)
                {
                    // Try to fetch it or something, if that fails navigate back
                }

                TradeInfo = tradeInfo!;

                UpdateState();

                NotificationSingleton.OnTradeUpdate += HandleTradeUpdate;
                
                break;
            }
            catch
            {

            }

            await Task.Delay(5_000);
        }

        await base.OnInitializedAsync();
    }

    private void UpdateState()
    {
        // Stupid hack but IsMyOffer is always false, will have to fix this
        if (TradeInfo.Offer.OwnerNodeAddress != TradeInfo.TradePeerNodeAddress)
        {
            TradeInfo.Offer.IsMyOffer = true;
        }

        IsBuyer = (TradeInfo.Offer.IsMyOffer && TradeInfo.Offer.Direction == "BUY") || (!TradeInfo.Offer.IsMyOffer && TradeInfo.Offer.Direction == "SELL");

        switch (TradeInfo.State)
        {
            case "ARBITRATOR_PUBLISHED_DEPOSIT_TXS":
            case "DEPOSIT_TXS_SEEN_IN_NETWORK":
            case "DEPOSIT_TXS_CONFIRMED_IN_BLOCKCHAIN":
                SellerState = 1;
                BuyerState = 1;
                break;
            case "DEPOSIT_TXS_UNLOCKED_IN_BLOCKCHAIN":
                SellerState = 2;
                BuyerState = 2;
                break;
            case "BUYER_CONFIRMED_PAYMENT_SENT":
            case "BUYER_SENT_PAYMENT_SENT_MSG":
            case "BUYER_SAW_ARRIVED_PAYMENT_SENT_MSG":
                BuyerState = 2;
                SellerState = TradeInfo.IsPayoutPublished ? 4 : 3;
                break;
            case "BUYER_STORED_IN_MAILBOX_PAYMENT_SENT_MSG":
            case "SELLER_RECEIVED_PAYMENT_SENT_MSG":
                BuyerState = 3;
                break;
            case "BUYER_SEND_FAILED_PAYMENT_SENT_MSG":
                BuyerState = 2;
                break;
            case "SELLER_SENT_PAYMENT_RECEIVED_MSG":
                BuyerState = 4;
                SellerState = TradeInfo.IsPayoutPublished ? 4 : 3;
                break;
            case "SELLER_CONFIRMED_PAYMENT_RECEIPT":
            case "SELLER_SEND_FAILED_PAYMENT_RECEIVED_MSG":
            case "SELLER_STORED_IN_MAILBOX_PAYMENT_RECEIVED_MSG":
            case "SELLER_SAW_ARRIVED_PAYMENT_RECEIVED_MSG":
            case "BUYER_RECEIVED_PAYMENT_RECEIVED_MSG":
                SellerState = TradeInfo.IsPayoutPublished ? 4 : 3;
                break;
            default: break;
        }
    }

    public async void HandleTradeUpdate(TradeInfo tradeInfo)
    {
        await InvokeAsync(() => {
            if (TradeInfo.TradeId == tradeInfo.TradeId)
            {
                TradeInfo = tradeInfo;
            }
            
            UpdateState();
            StateHasChanged();
        });
    }

    public async Task ConfirmPaymentReceivedAsync(string tradeId)
    {
        using var grpcChannelHelper = new GrpcChannelHelper();
        var tradesClient = new TradesClient(grpcChannelHelper.Channel);

        var confirmPaymentReceivedResponse = await tradesClient.ConfirmPaymentReceivedAsync(new ConfirmPaymentReceivedRequest
        {
            TradeId = tradeId
        });

        var getTradeResponse = await tradesClient.GetTradeAsync(new GetTradeRequest { TradeId = tradeId });

        NotificationSingleton.TradeInfos.AddOrUpdate(tradeId, getTradeResponse.Trade, (key, old) => getTradeResponse.Trade);
        TradeInfo = getTradeResponse.Trade;

        UpdateState();
    }

    public async Task ConfirmPaymentSentAsync(string tradeId)
    {
        using var grpcChannelHelper = new GrpcChannelHelper();
        var tradesClient = new TradesClient(grpcChannelHelper.Channel);

        var confirmPaymentReceivedResponse = await tradesClient.ConfirmPaymentSentAsync(new ConfirmPaymentSentRequest
        {
            TradeId = tradeId
        });

        // Daemon does not notify this update so we fetch again to re-sync - might be worth having the singleton fetch periodically
        var getTradeResponse = await tradesClient.GetTradeAsync(new GetTradeRequest { TradeId = tradeId });

        NotificationSingleton.TradeInfos.AddOrUpdate(tradeId, getTradeResponse.Trade, (key, old) => getTradeResponse.Trade);
        TradeInfo = getTradeResponse.Trade;

        UpdateState();
    }

    public async Task CompleteTradeAsync(string tradeId)
    {
        using var grpcChannelHelper = new GrpcChannelHelper();
        var tradesClient = new TradesClient(grpcChannelHelper.Channel);

        var completeTradeResponse = await tradesClient.CompleteTradeAsync(new CompleteTradeRequest
        {
            TradeId = tradeId
        });

        NavigationManager.NavigateTo("Trades");
    }

    public void Dispose()
    {
        NotificationSingleton.OnTradeUpdate -= HandleTradeUpdate;
    }
}
