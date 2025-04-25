using Grpc.Core;
using Haveno.Proto.Grpc;
using Manta.Extensions;
using Manta.Helpers;
using Manta.Services;
using Protobuf;
using System.Collections.Concurrent;
using System.Diagnostics;

using static Haveno.Proto.Grpc.GetTradesRequest.Types;
using static Haveno.Proto.Grpc.NotificationMessage.Types;
using static Haveno.Proto.Grpc.Notifications;
using static Haveno.Proto.Grpc.Trades;

namespace Manta.Singletons;

public class NotificationSingleton
{
    private CancellationTokenSource _cancellationTokenSource = new();
    private DateTime _lastMessageTime = new();
    private readonly INotificationManagerService _notificationManagerService;

    public event Action<ChatMessage>? OnChatMessage;
    public event Action<TradeInfo>? OnTradeUpdate;

    public ConcurrentDictionary<string, TradeInfo> TradeInfos { get; private set; } = [];

    public TaskCompletionSource<bool> InitializedTCS { get; private set; } = new();

    public NotificationSingleton(INotificationManagerService notificationManagerService)
    {
        _notificationManagerService = notificationManagerService;
        Task.Run(FetchInitial);
    }

    public async Task BackgroundSync()
    {
        _notificationManagerService.SendNotification($"BackgroundSync ran at {DateTime.Now}", "BackgroundSync");

        for (int i = 0; i < 2; i++)
        {
            try
            {
                using var grpcChannelHelper = new GrpcChannelHelper();
                var tradesClient = new TradesClient(grpcChannelHelper.Channel);

                var tradesResponse = await tradesClient.GetTradesAsync(new GetTradesRequest
                {
                    Category = Category.Open
                });

                _notificationManagerService.SendNotification($"count: {tradesResponse.Trades.Count}", "count");

                List<TradeInfo> updatedTrades = [];
                foreach (var trade in tradesResponse.Trades)
                {
                    TradeInfos.AddOrUpdate(trade.TradeId, trade, (key, old) => 
                    { 
                        updatedTrades.Add(trade); 
                        return trade;
                    });
                }

                //_notificationManagerService.SendNotification($"count: {updatedTrades.Count}", "count");

                // Not very efficient
                foreach (var trade in updatedTrades)
                {
                    GetChatMessagesReply? response = null;

                    for (int j = 0; j < 2; j++)
                    {
                        try
                        {
                            response = await tradesClient.GetChatMessagesAsync(new GetChatMessagesRequest { TradeId = trade.TradeId });
                            break;
                        }
                        catch (RpcException e)
                        {
                            Console.WriteLine(e);
                            await Task.Delay(1000);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }

                    if (response is null)
                    {
                        Debug.WriteLine("response is null");
                        continue;
                    }

                    var tempLastMessageTime = DateTime.MinValue;

                    foreach (var message in response.Message.Where(x => x.Date.ToDateTime() > _lastMessageTime))
                    {
                        var isMyMessage = message.SenderNodeAddress.HostName.Split(".")[0] != trade.TradePeerNodeAddress.Split(".")[0] && message.SenderNodeAddress.HostName.Split(".")[0] != trade.ArbitratorNodeAddress.Split(".")[0];
                        if (isMyMessage)
                            continue;

                        var date = message.Date.ToDateTime();
                        if (date > tempLastMessageTime)
                            tempLastMessageTime = date;

                        _notificationManagerService.SendNotification($"New message for trade {new string(trade.TradeId.Split('-')[0].ToArray())}", message.Message);
                    }

                    if (tempLastMessageTime != DateTime.MinValue)
                        _lastMessageTime = tempLastMessageTime;
                }

                break;
            }
            catch
            {
                if (i == 1)
                {

                }
            }

            await Task.Delay(5_000);
        }
    }

    public async Task FetchInitial()
    {
        while (true)
        {
            try
            {
                using var grpcChannelHelper = new GrpcChannelHelper();
                var tradesClient = new TradesClient(grpcChannelHelper.Channel);

                var tradesResponse = await tradesClient.GetTradesAsync(new GetTradesRequest 
                {
                    Category = Category.Open // Does nothing but has to be set
                });

                _lastMessageTime = DateTime.UtcNow;

                TradeInfos = new(tradesResponse.Trades.ToDictionary(x => x.TradeId, x => x));

                break;
            }
            catch
            {

            }

            await Task.Delay(5_000);
        }

        _ = Task.Run(PollTrades);
    }

    private async Task PollTrades()
    {
        start: 
        var grpcChannelHelper = new GrpcChannelHelper(noTimeout: true);
        var notificationClient = new NotificationsClient(grpcChannelHelper.Channel);
        
        AsyncServerStreamingCall<NotificationMessage> registerResponse;

        while (true)
        {
            try
            {
                registerResponse = notificationClient.RegisterNotificationListener(new RegisterNotificationListenerRequest(), cancellationToken: _cancellationTokenSource.Token);
                // Inital fetch has happened, and we have registered so we get updates.
                // Consumers can now be sure that data is available
                InitializedTCS.SetResult(true);

                // TODO parse this, misses first response otherwise
                var metadata = await registerResponse.ResponseHeadersAsync;
                break;
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch
            {

            }

            await Task.Delay(1_000);
        }

        while (true)
        {
            try
            {
                var metadata = await registerResponse.ResponseHeadersAsync;

                await foreach (var response in registerResponse.ResponseStream.ReadAllAsync(_cancellationTokenSource.Token))
                {
                    switch (response.Type) 
                    {
                        case NotificationType.ChatMessage:
                            var date = response.ChatMessage.Date.ToDateTime();
                            if (date > _lastMessageTime)
                                _lastMessageTime = date;

                            OnChatMessage?.Invoke(response.ChatMessage);
                            _notificationManagerService.SendNotification($"New message for trade {new string(response.ChatMessage.TradeId.Split('-')[0].ToArray())}", response.ChatMessage.Message);
                            break;
                        case NotificationType.TradeUpdate:
                            TradeInfos.AddOrUpdate(response.Trade.TradeId, response.Trade, (key, old) => response.Trade);

                            OnTradeUpdate?.Invoke(response.Trade);
                            _notificationManagerService.SendNotification($"Trade {response.Trade.ShortId} updated", response.Message);
                            break;
                        default: break;
                    }

                }
            }
            catch (TaskCanceledException)
            {
                registerResponse.Dispose();
                grpcChannelHelper.Dispose();
                return;
            }
            catch (Exception e) // Don't catch all exceptions...
            {
                Console.WriteLine(e);
                registerResponse.Dispose();
                grpcChannelHelper.Dispose();
                goto start;
            }
        }
    }
}
