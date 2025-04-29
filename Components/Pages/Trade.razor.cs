using Haveno.Proto.Grpc;
using Manta.Extensions;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Protobuf;

using static Haveno.Proto.Grpc.Disputes;
using static Haveno.Proto.Grpc.PaymentAccounts;
using static Haveno.Proto.Grpc.Trades;

namespace Manta.Components.Pages;

public partial class Trade : ComponentBase, IDisposable
{
    [Parameter]
    [SupplyParameterFromQuery]
    public string TradeId { get; set; } = string.Empty;
    public TradeInfo TradeInfo { get; set; } = default!;
    public PaymentMethod? PaymentMethod { get; set; }
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    public int SellerState { get; set; }
    public int BuyerState { get; set; }
    public bool IsBuyer { get; set; }
    public bool IsNotCompletedInTime { get; set; }
    public TimeSpan MaxTradePeriod { get; set; }
    public DateTime TradeExpiresDateUTC { get; set; }
    public bool IsFetching { get; set; }

    public string[] BuyerSteps { get; set; } = ["Wait for blockchain confirmations", "Start payment", "Wait until payment arrived", "Completed"];
    public string[] SellerSteps { get; set; } = ["Wait for blockchain confirmations", "Wait until payment has been sent", "Confirm payment received", "Completed"];

    public CancellationTokenSource CancellationTokenSource = new();
    public Task? FetchTradeTask;

    public string? DisputeMessage { get; set; }

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

                if (PaymentMethod is null)
                {
                    using var paymentAccountChannel = new GrpcChannelHelper();
                    var paymentAccountsClient = new PaymentAccountsClient(paymentAccountChannel.Channel);

                    var paymentMethodsResponse = await paymentAccountsClient.GetPaymentMethodsAsync(new GetPaymentMethodsRequest());

                    PaymentMethod = paymentMethodsResponse.PaymentMethods.FirstOrDefault(x => x.Id == TradeInfo.Offer.PaymentMethodId);
                }

                if (PaymentMethod is not null)
                {
                    MaxTradePeriod = new TimeSpan(PaymentMethod.MaxTradePeriod * 10_000);

                    // This is wrong, should be from the time of the first blockchain conf
                    TradeExpiresDateUTC = TradeInfo.Date.ToDateTime().Add(MaxTradePeriod);

                    // Really only need to do this if trade is not completed - fix 
                    var utcNow = DateTime.UtcNow;

                    if (utcNow >= TradeExpiresDateUTC)
                    {
                        IsNotCompletedInTime = true;
                    }
                    else
                    {
                        _ = Task.Run(async () => {
                            await Task.Delay(TradeExpiresDateUTC.Subtract(utcNow));

                            IsNotCompletedInTime = true;

                            await InvokeAsync(StateHasChanged);
                        });
                    }
                }

                UpdateTradeState();

                NotificationSingleton.OnTradeUpdate += HandleTradeUpdate;

                FetchTradeTask = FetchTradeAsync();
                break;
            }
            catch (Exception ex)
            {

            }

            await Task.Delay(5_000);
        }

        await base.OnInitializedAsync();
    }

    private void UpdateTradeState()
    {
        // Stupid hack but IsMyOffer is always false, will have to fix this
        if (TradeInfo.Offer.OwnerNodeAddress != TradeInfo.TradePeerNodeAddress)
        {
            TradeInfo.Offer.IsMyOffer = true;
        }

        IsBuyer = (TradeInfo.Offer.IsMyOffer && TradeInfo.Offer.Direction == "BUY") || (!TradeInfo.Offer.IsMyOffer && TradeInfo.Offer.Direction == "SELL");

        switch (TradeInfo.DisputeState) 
        {
            case "DISPUTE_CLOSED":
                DisputeMessage = "This trade was resolved by arbitration";
                break;
            case "MEDIATION_CLOSED":
                DisputeMessage = "This trade was resolved by mediation";
                break;
            case "REFUND_REQUEST_CLOSED":
                break;
            default: break;
        }

        switch (TradeInfo.PayoutState)
        {
            case "PAYOUT_PUBLISHED":
            case "PAYOUT_CONFIRMED":
            case "PAYOUT_UNLOCKED":
                SellerState = 4;
                BuyerState = 4;
                return;
            default:
                break;
        }

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

    // Daemon does not send notif when seller has marked payment as received?
    public async void HandleTradeUpdate(TradeInfo tradeInfo)
    {
        await InvokeAsync(() => {
            try
            {
                TradeInfo = NotificationSingleton.TradeInfos[TradeId];
                UpdateTradeState();
                StateHasChanged();
            }
            catch (Exception e)
            {

            }
        });
    }

    public async Task ConfirmPaymentReceivedAsync(string tradeId)
    {
        IsFetching = true;

        using var grpcChannelHelper = new GrpcChannelHelper(noTimeout: true);
        var tradesClient = new TradesClient(grpcChannelHelper.Channel);

        var confirmPaymentReceivedResponse = await tradesClient.ConfirmPaymentReceivedAsync(new ConfirmPaymentReceivedRequest
        {
            TradeId = tradeId
        });

        var getTradeResponse = await tradesClient.GetTradeAsync(new GetTradeRequest { TradeId = tradeId });

        NotificationSingleton.TradeInfos.AddOrUpdate(tradeId, getTradeResponse.Trade, (key, old) => getTradeResponse.Trade);
        TradeInfo = getTradeResponse.Trade;

        UpdateTradeState();

        IsFetching = false;
    }

    public async Task ConfirmPaymentSentAsync(string tradeId)
    {
        IsFetching = true;

        using var grpcChannelHelper = new GrpcChannelHelper(noTimeout: true);
        var tradesClient = new TradesClient(grpcChannelHelper.Channel);

        var confirmPaymentReceivedResponse = await tradesClient.ConfirmPaymentSentAsync(new ConfirmPaymentSentRequest
        {
            TradeId = tradeId
        });

        IsFetching = false;
    }

    public async Task CompleteTradeAsync(string tradeId)
    {
        using var grpcChannelHelper = new GrpcChannelHelper();
        var tradesClient = new TradesClient(grpcChannelHelper.Channel);

        var completeTradeResponse = await tradesClient.CompleteTradeAsync(new CompleteTradeRequest
        {
            TradeId = tradeId
        });

        var cloneTradeInfo = TradeInfo.Clone();
        cloneTradeInfo.IsCompleted = true;

        NotificationSingleton.TradeInfos.TryUpdate(TradeId, cloneTradeInfo, TradeInfo);

        NavigationManager.NavigateTo("Trades");
    }

    // Daemon does not notify for every change so we will need to poll for now
    public async Task FetchTradeAsync()
    {
        while (true)
        {
            try
            {
                using var grpcChannelHelper = new GrpcChannelHelper();
                var tradesClient = new TradesClient(grpcChannelHelper.Channel);

                var getTradeResponse = await tradesClient.GetTradeAsync(new GetTradeRequest
                {
                    TradeId = TradeId
                }, cancellationToken: CancellationTokenSource.Token);

                if (NotificationSingleton.TradeInfos.TryUpdate(TradeId, getTradeResponse.Trade, TradeInfo))
                {
                    TradeInfo = getTradeResponse.Trade;
                    UpdateTradeState();
                    StateHasChanged();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            await Task.Delay(5_000, CancellationTokenSource.Token);
        }
    }

    public async Task OpenDisputeAsync(string tradeId)
    {
        using var grpcChannelHelper = new GrpcChannelHelper();
        var disputesClient = new DisputesClient(grpcChannelHelper.Channel);

        var response = await disputesClient.OpenDisputeAsync(new OpenDisputeRequest { TradeId = tradeId });

        NavigationManager.NavigateTo("trades?title=Trades&SelectedTabIndex=2");
    }

    public async Task GoToDisputeAsync()
    {
        NavigationManager.NavigateTo($"/chat?disputeTradeId={TradeInfo.TradeId}&title=Trade%20{TradeInfo.ShortId}%20chat&arbitrator={TradeInfo.ArbitratorNodeAddress.Split(".")[0]}&tradePeer={TradeInfo.TradePeerNodeAddress.Split(".")[0]}&myAddress={TradeInfo.Offer.OwnerNodeAddress.Split(".")[0]}");
    }

    public void Dispose()
    {
        NotificationSingleton.OnTradeUpdate -= HandleTradeUpdate;
        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();
    }
}
