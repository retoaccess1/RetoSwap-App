using Haveno.Proto.Grpc;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Protobuf;

namespace Manta.Components.Pages;

public partial class Trades : ComponentBase, IDisposable
{
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;

    public int SelectedTabIndex { get; set; }
    public List<TradeInfo> FilteredTradeInfos { get 
        {
            switch(SelectedTabIndex)
            {
                case 0:
                    return NotificationSingleton.TradeInfos.Values.Where(x => !x.IsCompleted).ToList();
                case 1:
                    return NotificationSingleton.TradeInfos.Values.Where(x => x.IsCompleted).ToList();
                default: 
                    return [];
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        NotificationSingleton.OnChatMessage += HandleChatMessage;
        NotificationSingleton.OnTradeUpdate += HandleTradeUpdate;

        await base.OnInitializedAsync();
    }

    public async void HandleChatMessage(ChatMessage chatMessage)
    {
        await InvokeAsync(() => {
            StateHasChanged();
        });
    }

    public async void HandleTradeUpdate(TradeInfo tradeInfo)
    {
        await InvokeAsync(() => {
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        NotificationSingleton.OnChatMessage -= HandleChatMessage;
        NotificationSingleton.OnTradeUpdate -= HandleTradeUpdate;
    }
}
