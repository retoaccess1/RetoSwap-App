using HavenoSharp.Models;
using HavenoSharp.Singletons;
using Manta.Services;
using System.Collections.Concurrent;

namespace Manta.Singletons;

public class NotificationSingleton
{
    private readonly HavenoNotificationSingleton _havenoNotificationSingleton;
    private readonly INotificationManagerService _notificationManagerService;

    public event Action<ChatMessage>? OnChatMessage;
    public event Action<TradeInfo>? OnTradeUpdate;

    public ConcurrentDictionary<string, TradeInfo> TradeInfos => _havenoNotificationSingleton.TradeInfos;
    public TaskCompletionSource<bool> InitializedTCS => _havenoNotificationSingleton.IsInitialized;

    public NotificationSingleton(INotificationManagerService notificationManagerService, HavenoNotificationSingleton havenoNotificationSingleton)
    {
        _notificationManagerService = notificationManagerService;
        _havenoNotificationSingleton = havenoNotificationSingleton;

        _havenoNotificationSingleton.NotificationMessageReceived += HandleNotificationMessageReceived;
    }

    private void HandleNotificationMessageReceived(NotificationMessage notificationMessage)
    {
        switch (notificationMessage.Type)
        {
            case NotificationType.ChatMessage:
                {
                    OnChatMessage?.Invoke(notificationMessage.ChatMessage);

                    if (TradeInfos.TryGetValue(notificationMessage.ChatMessage.TradeId, out var tradeInfo))
                    {
                        if (notificationMessage.ChatMessage.Type == SupportType.Arbitration)
                        {
                            _notificationManagerService.SendNotification($"New message for trade {new string(notificationMessage.ChatMessage.TradeId.Split('-')[0].ToArray())}", notificationMessage.ChatMessage.Message, $"trades/{tradeInfo.TradeId}/chat?disputeTradeId={tradeInfo.TradeId}&title=Trade%20{tradeInfo.ShortId}%20dispute%20chat&arbitrator={tradeInfo.ArbitratorNodeAddress.Split(".")[0]}&tradePeer={tradeInfo.TradePeerNodeAddress.Split(".")[0]}&myAddress={tradeInfo.Offer.OwnerNodeAddress.Split(".")[0]}");
                        }
                        else
                        {
                            _notificationManagerService.SendNotification($"New message for trade {new string(notificationMessage.ChatMessage.TradeId.Split('-')[0].ToArray())}", notificationMessage.ChatMessage.Message, $"trades/{tradeInfo.TradeId}/chat?tradeId={tradeInfo.TradeId}&title=Trade%20{tradeInfo.ShortId}%20chat&arbitrator={tradeInfo.ArbitratorNodeAddress.Split(".")[0]}&tradePeer={tradeInfo.TradePeerNodeAddress.Split(".")[0]}&myAddress={tradeInfo.Offer.OwnerNodeAddress.Split(".")[0]}");
                        }
                    }
                    else
                    {
                        _notificationManagerService.SendNotification($"New message for trade {new string(notificationMessage.ChatMessage.TradeId.Split('-')[0].ToArray())}", notificationMessage.ChatMessage.Message);
                    }
                }
                break;
            case NotificationType.TradeUpdate:
                {
                    OnTradeUpdate?.Invoke(notificationMessage.Trade);

                    var tradeInfo = notificationMessage.Trade;
                    _notificationManagerService.SendNotification($"Trade {tradeInfo.ShortId} updated", notificationMessage.Message, $"trades/{tradeInfo.TradeId}/trade?tradeId={tradeInfo.TradeId}&title=Trade%20{@tradeInfo.ShortId}");
                }
                break;
            default: break;
        }
    }

    public void Start(CancellationToken cancellationToken = default)
    {
        _havenoNotificationSingleton.Start(cancellationToken);
    }

    public async Task StopNotificationListenerAsync()
    {
        await _havenoNotificationSingleton.StopNotificationListenerAsync();
    }

    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        await _havenoNotificationSingleton.PollAsync(cancellationToken);
    }
}
