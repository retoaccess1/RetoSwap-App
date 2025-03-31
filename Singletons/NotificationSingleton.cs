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

// Might need to move this to a background service 
public class NotificationSingleton
{
    private readonly INotificationManagerService _notificationManagerService;

    public event Action<ChatMessage>? OnChatMessage;
    public event Action<TradeInfo>? OnTradeUpdate;

    public ConcurrentDictionary<string, TradeInfo> TradeInfos { get; private set; } = [];
    public List<ChatMessage> ChatMessages { get; private set; } = [];

    // Activated singleton?
    public NotificationSingleton(INotificationManagerService notificationManagerService)
    {
        _notificationManagerService = notificationManagerService;
        Task.Run(FetchInitial);
    }

    private async Task FetchInitial()
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

                await registerResponse.ResponseHeadersAsync;
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
                            ChatMessages.Add(response.ChatMessage);
                            OnChatMessage?.Invoke(response.ChatMessage);
                            _notificationManagerService.SendNotification($"New message for trade {new string(response.ChatMessage.TradeId.Split('-')[0].ToArray())}", response.ChatMessage.Message);
                            break;
                        case NotificationType.TradeUpdate:
                            TradeInfos.AddOrUpdate(response.Trade.TradeId, response.Trade, (key, old) => response.Trade);

                            OnTradeUpdate?.Invoke(response.Trade);
                            _notificationManagerService.SendNotification($"Trade {response.Trade.ShortId} updated", response.Trade.ShortId);
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
