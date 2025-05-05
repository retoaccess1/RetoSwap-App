using Haveno.Proto.Grpc;
using Manta.Extensions;
using Manta.Helpers;
using Manta.Models;

using static Haveno.Proto.Grpc.GetTradeStatistics;

namespace Manta.Singletons;

public class TradeStatisticsSingleton
{
    private int _delay = 5_000;
    private CancellationTokenSource _cancellationTokenSource = new();

    public event Action<bool>? OnTradeStatisticsFetch;
    public List<TradeStatistic> TradeStatistics { get; private set; } = [];
    public Task? FetchTradeStatisticsTask {get; private set; }

    public TaskCompletionSource<bool> InitializedTCS { get; private set; } = new();

    public TradeStatisticsSingleton()
    {
        FetchTradeStatisticsTask = Task.Run(FetchTradeStatisticsAsync);
    }

    public async Task FetchTradeStatisticsAsync()
    {
        while (true) 
        {
            try
            {
                OnTradeStatisticsFetch?.Invoke(true);

                var grpcChannelHelper = new GrpcChannelHelper();
                var tradeStatisticsClient = new GetTradeStatisticsClient(grpcChannelHelper.Channel);

                // Really should be a way to fetch stats after a certain date
                var response = await tradeStatisticsClient.GetTradeStatisticsAsync(new GetTradeStatisticsRequest(), cancellationToken: _cancellationTokenSource.Token);

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
                }).ToList();

                if (!InitializedTCS.Task.IsCompleted)
                {
                    InitializedTCS.SetResult(true);
#if !DEBUG
                    _delay = 60_000;
#endif
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                //Console.WriteLine(e);
            }
            finally
            {
                OnTradeStatisticsFetch?.Invoke(false);
            }

            await Task.Delay(_delay, _cancellationTokenSource.Token);
        }
    }
}
