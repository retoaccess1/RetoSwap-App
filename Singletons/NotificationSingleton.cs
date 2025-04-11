using Grpc.Core;
using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Services;
using Protobuf;
using System.Collections.Concurrent;

using static Haveno.Proto.Grpc.GetTradesRequest.Types;
using static Haveno.Proto.Grpc.NotificationMessage.Types;
using static Haveno.Proto.Grpc.Notifications;
using static Haveno.Proto.Grpc.Trades;

namespace Manta.Singletons;

public class NotificationSingleton
{
    private DateTime _messagesLastUpdated = new();
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

                List<TradeInfo> updatedTrades = [];
                foreach (var trade in tradesResponse.Trades)
                {
                    TradeInfos.AddOrUpdate(trade.TradeId, trade, (key, old) => 
                    { 
                        updatedTrades.Add(trade); 
                        return trade;  
                    });
                }

                foreach (var trade in updatedTrades)
                {
                    var response = await tradesClient.GetChatMessagesAsync(new GetChatMessagesRequest { TradeId = trade.TradeId });

                    // TODO also check its not our message
                    foreach(var message in response.Message.Where(x => new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(x.Date) > _messagesLastUpdated))
                    {
                        //OnChatMessage?.Invoke(message);
                        //_notificationManagerService.SendNotification($"New message for trade {new string(trade.TradeId.Split('-')[0].ToArray())}", message.Message);
                    }
                }

                _messagesLastUpdated = DateTime.UtcNow;

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

                _messagesLastUpdated = DateTime.UtcNow;

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
                registerResponse = notificationClient.RegisterNotificationListener(new RegisterNotificationListenerRequest());
                // Inital fetch has happened, and we have registered so we get updates.
                // Consumers can now be sure that data is available
                InitializedTCS.SetResult(true);

                var metadata = await registerResponse.ResponseHeadersAsync;
                break;
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

                await foreach (var response in registerResponse.ResponseStream.ReadAllAsync())
                {
                    switch (response.Type) 
                    {
                        case NotificationType.ChatMessage:
                            _messagesLastUpdated = DateTime.UtcNow;
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
