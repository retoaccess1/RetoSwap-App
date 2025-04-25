using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Protobuf;

using static Haveno.Proto.Grpc.Disputes;

namespace Manta.Components.Pages;

public partial class Trades : ComponentBase, IDisposable
{
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;

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
                    return NotificationSingleton.TradeInfos.Values.Where(x => !x.IsCompleted).ToList();
                case 1:
                    return NotificationSingleton.TradeInfos.Values.Where(x => x.IsCompleted).ToList();
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
        using var grpcChannelHelper = new GrpcChannelHelper();
        var disputesClient = new DisputesClient(grpcChannelHelper.Channel);

        var response = await disputesClient.GetDisputesAsync(new GetDisputesRequest());

        Disputes = [.. response.Disputes];
    }

    public void GetDisputes()
    {
        using var grpcChannelHelper = new GrpcChannelHelper();
        var disputesClient = new DisputesClient(grpcChannelHelper.Channel);

        var response = disputesClient.GetDisputes(new GetDisputesRequest());

        Disputes = [.. response.Disputes];
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
