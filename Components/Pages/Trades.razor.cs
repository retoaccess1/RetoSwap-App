using HavenoSharp.Models;
using HavenoSharp.Services;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;

namespace Manta.Components.Pages;

public partial class Trades : ComponentBase, IDisposable
{
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public IHavenoDisputeService DisputeService { get; set; } = default!;

    [Parameter]
    [SupplyParameterFromQuery]
    public int SelectedTabIndex 
    { 
        get;
        set
        {
            field = value;
            if (value == 2)
            {
                GetDisputes();
            }
        }
    }

    public List<TradeInfo> FilteredTradeInfos 
    { 
        get 
        {
            switch(SelectedTabIndex)
            {
                case 0:
                    return NotificationSingleton.TradeInfos.Values.Where(x => !x.IsCompleted).OrderByDescending(x => x.Date).ToList();
                case 1:
                    return NotificationSingleton.TradeInfos.Values.Where(x => x.IsCompleted).OrderByDescending(x => x.Date).ToList();
                case 2:
                    return [];
                default: 
                    return [];
            }
        }
    }

    public List<Dispute> Disputes { get; set; } = [];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        if (!NotificationSingleton.InitializedTCS.Task.IsCompleted)
        {
            // Timeout needed
            await NotificationSingleton.InitializedTCS.Task;
        }

        NotificationSingleton.OnChatMessage += HandleChatMessage;
        NotificationSingleton.OnTradeUpdate += HandleTradeUpdate;

        await base.OnInitializedAsync();
    }

    public async Task GetDisputesAsync()
    {
        Disputes = await DisputeService.GetDisputesAsync();
    }

    public void GetDisputes()
    {
        Task.Run(GetDisputesAsync).GetAwaiter().GetResult();
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
