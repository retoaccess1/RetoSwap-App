using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Protobuf;

namespace Manta.Components.Pages;

public partial class Chat : ComponentBase, IDisposable
{
    [Parameter]
    [SupplyParameterFromQuery]
    public string TradeId { get; set; } = string.Empty;
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
    [Parameter]
    [SupplyParameterFromQuery]
    public string MyAddress { get; set; } = string.Empty;

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
            using var grpcChannelHelper = new GrpcChannelHelper();
            var tradesClient = new Haveno.Proto.Grpc.Trades.TradesClient(grpcChannelHelper.Channel);

            var request = new GetChatMessagesRequest
            {
                TradeId = TradeId
            };

            var messagesResponse = await tradesClient.GetChatMessagesAsync(request);

            // Might be worth caching these
            Messages = [.. messagesResponse.Message.Skip(1).OrderBy(x => x.Date)];

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
            using var grpcChannelHelper = new GrpcChannelHelper();
            var tradesClient = new Haveno.Proto.Grpc.Trades.TradesClient(grpcChannelHelper.Channel);

            var sendChatMessageResponse = await tradesClient.SendChatMessageAsync(new SendChatMessageRequest
            {
                TradeId = TradeId,
                Message = Message
            });

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
