using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Protobuf;

using static Haveno.Proto.Grpc.Trades;
using static Haveno.Proto.Grpc.Disputes;

namespace Manta.Components.Pages;

public partial class Chat : ComponentBase, IDisposable
{
    [Parameter]
    [SupplyParameterFromQuery]
    public string TradeId { get; set; } = string.Empty;
    [Parameter]
    [SupplyParameterFromQuery]
    public string DisputeTradeId { get; set; } = string.Empty;
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public IJSRuntime JS { get; set; } = default!;
    public List<ChatMessage> Messages { get; set; } = [];
    public string Message { get; set; } = string.Empty;

    [Parameter]
    [SupplyParameterFromQuery]
    public string Arbitrator { get; set; } = string.Empty;
    [Parameter]
    [SupplyParameterFromQuery]
    public string TradePeer { get; set; } = string.Empty;

    // Unused
    [Parameter]
    [SupplyParameterFromQuery]
    public string MyAddress { get; set; } = string.Empty;

    public string DisputeId { get; set; } = string.Empty;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await JS.InvokeVoidAsync("ScrollToEnd", "last-message");

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        try 
        {
            if (!string.IsNullOrEmpty(TradeId))
            {
                using var grpcChannelHelper = new GrpcChannelHelper();
                var tradesClient = new TradesClient(grpcChannelHelper.Channel);

                var request = new GetChatMessagesRequest
                {
                    TradeId = TradeId
                };

                var messagesResponse = await tradesClient.GetChatMessagesAsync(request);

                // Might be worth caching these
                Messages = [.. messagesResponse.Message.OrderBy(x => x.Date)];
            }
            else if (!string.IsNullOrEmpty(DisputeTradeId))
            {
                using var grpcChannelHelper = new GrpcChannelHelper();
                var disputesClient = new DisputesClient(grpcChannelHelper.Channel);

                var response = await disputesClient.GetDisputeAsync(new GetDisputeRequest { TradeId = DisputeTradeId });

                Messages = [.. response.Dispute.ChatMessage.OrderBy(x => x.Date)];
                DisputeId = response.Dispute.Id;
            }

            NotificationSingleton.OnChatMessage += HandleChatMessage;
        }
        catch
        {

        }

        await base.OnInitializedAsync();
    }

    public void HandleMessageChanged(ChangeEventArgs e)
    {
        Message = (string)e.Value!;
    }

    public async Task SendMessageAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(TradeId))
            {
                using var grpcChannelHelper = new GrpcChannelHelper();
                var tradesClient = new TradesClient(grpcChannelHelper.Channel);

                await tradesClient.SendChatMessageAsync(new SendChatMessageRequest
                {
                    TradeId = TradeId,
                    Message = Message
                });
            }
            else if (!string.IsNullOrEmpty(DisputeId))
            {
                using var grpcChannelHelper = new GrpcChannelHelper();
                var disputesClient = new DisputesClient(grpcChannelHelper.Channel);

                await disputesClient.SendDisputeChatMessageAsync(new SendDisputeChatMessageRequest 
                { 
                    DisputeId = DisputeId, 
                    Message = Message 
                });
            }

            // Better way of doing this?
            Messages.Add(new ChatMessage
            {
                Message = Message,
                Date = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                SenderNodeAddress = new NodeAddress
                {
                    HostName = ""
                }
            });

            Message = string.Empty;
        }
        catch (Exception e)
        {

        }
    }

    public async void HandleChatMessage(ChatMessage chatMessage)
    {
        await InvokeAsync(() => { 
            Messages.Add(chatMessage);
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        NotificationSingleton.OnChatMessage -= HandleChatMessage;
    }
}
