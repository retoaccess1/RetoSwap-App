using HavenoSharp.Models;
using HavenoSharp.Models.Requests;
using HavenoSharp.Services;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Manta.Components.Pages;

public partial class Chat : ComponentBase, IDisposable
{
    [Inject]
    public IHavenoTradeService TradeService { get; set; } = default!;
    [Inject]
    public IHavenoDisputeService DisputeService { get; set; } = default!;
    // TODO update to use route
    [Parameter]
    public string Id { get; set; } = string.Empty;
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
                var messages = await TradeService.GetChatMessagesAsync(TradeId);

                // Might be worth caching these
                Messages = [.. messages.OrderBy(x => x.Date)];
            }
            else if (!string.IsNullOrEmpty(DisputeTradeId))
            {
                var dispute = await DisputeService.GetDisputeAsync(DisputeTradeId);

                Messages = [.. dispute.ChatMessages.OrderBy(x => x.Date)];
                DisputeId = dispute.Id;
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
                await TradeService.SendChatMessageAsync(TradeId, Message);
            }
            else if (!string.IsNullOrEmpty(DisputeId))
            {
                await DisputeService.SendDisputeChatMessageAsync(new SendDisputeChatMessageRequest 
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
        catch (Exception)
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
