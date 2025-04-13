using Haveno.Proto.Grpc;
using Manta.Extensions;
using Manta.Helpers;
using Manta.Models;

using static Haveno.Proto.Grpc.GetTradeStatistics;

namespace Manta.Singletons;

public class TradeStatisticsSingleton
{
    private readonly GetTradeStatisticsClient _tradeStatisticsClient;
    private readonly GrpcChannelHelper _grpcChannelHelper;
    private CancellationTokenSource _cancellationTokenSource = new();

    public event Action<bool>? OnTradeStatisticsFetch;
    public List<TradeStatistic> TradeStatistics { get; private set; } = [];
    public Task? FetchTradeStatisticsTask {get; private set; }

    public TradeStatisticsSingleton()
    {
        _grpcChannelHelper = new();
        _tradeStatisticsClient = new GetTradeStatisticsClient(_grpcChannelHelper.Channel);

        FetchTradeStatisticsTask = Task.Run(FetchTradeStatisticsAsync);
    }

    public async Task FetchTradeStatisticsAsync()
    {
        while (true) 
        {
            try
            {
                OnTradeStatisticsFetch?.Invoke(true);

                // Really should be a way to fetch stats after a certain date
                var response = await _tradeStatisticsClient.GetTradeStatisticsAsync(new GetTradeStatisticsRequest(), cancellationToken: _cancellationTokenSource.Token);

                TradeStatistics = response.TradeStatistics.Select(x => new TradeStatistic
                {
                    Amount = x.Amount,
                    Arbitrator = x.Arbitrator,
                    Currency = x.Currency,
                    Date = x.Date.ToDateTime(),
                    ExtraData = x.ExtraData.ToDictionary(),
                    Hash = x.Hash.ToByteArray(),
                    MakerDepositTxId = x.MakerDepositTxId,
                    PaymentMethod = x.PaymentMethod,
                    Price = x.Price,
                    TakerDepositTxId = x.TakerDepositTxId
                }).OrderBy(x => x.Date).ToList();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                OnTradeStatisticsFetch?.Invoke(false);
            }

#if DEBUG
            await Task.Delay(5_000, _cancellationTokenSource.Token);
#else
            await Task.Delay(60_000, _cancellationTokenSource.Token);
#endif
        }
    }
}
