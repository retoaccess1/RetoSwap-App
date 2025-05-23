using HavenoSharp.Models;
using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Extensions;
using Manta.Services;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Manta.Singletons;

public class NotificationSingleton
{
    private CancellationTokenSource _cancellationTokenSource = new();
    private DateTime _lastMessageTime = new();
    private readonly INotificationManagerService _notificationManagerService;
    private readonly IServiceProvider _serviceProvider;
    private readonly HavenoNotificationSingleton _havenoNotificationSingleton;

    public event Action<ChatMessage>? OnChatMessage;
    public event Action<TradeInfo>? OnTradeUpdate;

    public ConcurrentDictionary<string, TradeInfo> TradeInfos { get; private set; } = [];

    public TaskCompletionSource<bool> InitializedTCS { get; private set; } = new();

    public NotificationSingleton(INotificationManagerService notificationManagerService, IServiceProvider serviceProvider, HavenoNotificationSingleton havenoNotificationSingleton)
    {
        _serviceProvider = serviceProvider;
        _notificationManagerService = notificationManagerService;
        _havenoNotificationSingleton = havenoNotificationSingleton;
        Task.Run(FetchInitial);
    }

    public async Task BackgroundSync()
    {
        for (int i = 0; i < 2; i++)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var tradeService = _serviceProvider.GetRequiredService<IHavenoTradeService>();

                var trades = await tradeService.GetTradesAsync(Category.Open);

                List<TradeInfo> updatedTrades = [];
                foreach (var trade in trades)
                {
                    TradeInfos.AddOrUpdate(trade.TradeId, trade, (key, old) =>
                    {
                        updatedTrades.Add(trade);
                        return trade;
                    });
                }

                foreach (var trade in updatedTrades)
                {
                    List<ChatMessage>? chatMessages = null;

                    try
                    {
                        chatMessages = await tradeService.GetChatMessagesAsync(trade.TradeId);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        continue;
                    }

                    if (chatMessages is null)
                    {
                        Debug.WriteLine("response is null");
                        continue;
                    }

                    var tempLastMessageTime = DateTime.MinValue;

                    foreach (var message in chatMessages.Where(x => x.Date.ToDateTime() > _lastMessageTime))
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
            catch (Exception e)
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
                using var scope = _serviceProvider.CreateScope();
                var tradeService = _serviceProvider.GetRequiredService<IHavenoTradeService>();

                var trades = await tradeService.GetTradesAsync(Category.Open);

                _lastMessageTime = DateTime.UtcNow;

                TradeInfos = new(trades.ToDictionary(x => x.TradeId, x => x));

                break;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            await Task.Delay(2_000);
        }

        await RegisterListener();
    }

    private void HandleNotificationMessageReceived(NotificationMessage notificationMessage)
    {
        switch (notificationMessage.Type)
        {
            case NotificationType.ChatMessage:
                var date = notificationMessage.ChatMessage.Date.ToDateTime();
                if (date > _lastMessageTime)
                    _lastMessageTime = date;

                OnChatMessage?.Invoke(notificationMessage.ChatMessage);
                _notificationManagerService.SendNotification($"New message for trade {new string(notificationMessage.ChatMessage.TradeId.Split('-')[0].ToArray())}", notificationMessage.ChatMessage.Message);
                break;
            case NotificationType.TradeUpdate:
                TradeInfos.AddOrUpdate(notificationMessage.Trade.TradeId, notificationMessage.Trade, (key, old) => notificationMessage.Trade);

                OnTradeUpdate?.Invoke(notificationMessage.Trade);
                _notificationManagerService.SendNotification($"Trade {notificationMessage.Trade.ShortId} updated", notificationMessage.Message);
                break;
            default: break;
        }
    }

    private async Task RegisterListener()
    {
        _havenoNotificationSingleton.NotificationMessageReceived += HandleNotificationMessageReceived;
        await _havenoNotificationSingleton.RegisterNotificationListener(_cancellationTokenSource.Token);
    }
}
