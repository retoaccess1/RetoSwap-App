using Manta.Helpers;
using Microsoft.AspNetCore.Components;

using static Haveno.Proto.Grpc.Account;
using static Haveno.Proto.Grpc.DisputeAgents;
using static Haveno.Proto.Grpc.Disputes;
using static Haveno.Proto.Grpc.GetTradeStatistics;
using static Haveno.Proto.Grpc.Offers;
using static Haveno.Proto.Grpc.PaymentAccounts;
using static Haveno.Proto.Grpc.Price;
using static Haveno.Proto.Grpc.Trades;
using static Haveno.Proto.Grpc.Wallets;
using static Haveno.Proto.Grpc.XmrConnections;
using static Haveno.Proto.Grpc.XmrNode;

namespace Manta.Models;

// WIP not used yet
public class CustomComponentBase : ComponentBase, IDisposable
{
    private GrpcChannelHelper _grpcChannelHelper = new();

    private TradesClient? _tradesClient;
    public TradesClient TradesClient 
    {
        get 
        {
            _tradesClient ??= new TradesClient(_grpcChannelHelper.Channel);
            return _tradesClient;
        }
    }

    private WalletsClient? _walletsClient;
    public WalletsClient WalletsClient
    {
        get
        {
            _walletsClient ??= new WalletsClient(_grpcChannelHelper.Channel);
            return _walletsClient;
        }
    }

    private GetTradeStatisticsClient? _getTradeStatisticsClient;
    public GetTradeStatisticsClient GetTradeStatisticsClient
    {
        get
        {
            _getTradeStatisticsClient ??= new GetTradeStatisticsClient(_grpcChannelHelper.Channel);
            return _getTradeStatisticsClient;
        }
    }

    private PriceClient? _priceClient;
    public PriceClient PriceClient
    {
        get
        {
            _priceClient ??= new PriceClient(_grpcChannelHelper.Channel);
            return _priceClient;
        }
    }

    private PaymentAccountsClient? _paymentAccountsClient;
    public PaymentAccountsClient PaymentAccountsClient
    {
        get
        {
            _paymentAccountsClient ??= new PaymentAccountsClient(_grpcChannelHelper.Channel);
            return _paymentAccountsClient;
        }
    }

    private OffersClient? _offersClient;
    public OffersClient OffersClient
    {
        get
        {
            _offersClient ??= new OffersClient(_grpcChannelHelper.Channel);
            return _offersClient;
        }
    }

    private XmrNodeClient? _xmrNodeClient;
    public XmrNodeClient XmrNodeClient
    {
        get
        {
            _xmrNodeClient ??= new XmrNodeClient(_grpcChannelHelper.Channel);
            return _xmrNodeClient;
        }
    }

    private XmrConnectionsClient? _xmrConnectionsClient;
    public XmrConnectionsClient XmrConnectionsClient
    {
        get
        {
            _xmrConnectionsClient ??= new XmrConnectionsClient(_grpcChannelHelper.Channel);
            return _xmrConnectionsClient;
        }
    }

    private DisputeAgentsClient? _disputeAgentsClient;
    public DisputeAgentsClient DisputeAgentsClient
    {
        get
        {
            _disputeAgentsClient ??= new DisputeAgentsClient(_grpcChannelHelper.Channel);
            return _disputeAgentsClient;
        }
    }

    private DisputesClient? _disputesClient;
    public DisputesClient DisputesClient
    {
        get
        {
            _disputesClient ??= new DisputesClient(_grpcChannelHelper.Channel);
            return _disputesClient;
        }
    }

    private AccountClient? _accountClient;
    public AccountClient AccountClient
    {
        get
        {
            _accountClient ??= new AccountClient(_grpcChannelHelper.Channel);
            return _accountClient;
        }
    }

    public bool IsFetching { get; set; }

    public CustomComponentBase()
    {

    }

    public void Dispose()
    {
        _grpcChannelHelper.Dispose();
    }
}
