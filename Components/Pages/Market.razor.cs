using Blazored.LocalStorage;
using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using static Haveno.Proto.Grpc.GetVersion;

namespace Manta.Components.Pages;

public partial class Market : ComponentBase, IDisposable
{
    public bool IsFetching { get; set; }
    public string Version { get; set; } = string.Empty;
    public WalletInfo? Balance { get; set; }
    public List<TradeStatistic> TradeStatistics { get; private set; } = [];

    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public IJSRuntime JS { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;
    [Inject]
    public TradeStatisticsSingleton TradeStatisticsSingleton { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        while (true)
        {
            try
            {
                IsFetching = true;
                StateHasChanged();

                using var grpcChannelHelper = new GrpcChannelHelper();
                var client = new GetVersionClient(grpcChannelHelper.Channel);

                var response = await client.GetVersionAsync(new GetVersionRequest());
                Version = response.Version;

                break;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            await Task.Delay(5_000);
        }

        TradeStatistics = TradeStatisticsSingleton.TradeStatistics;
        Balance = BalanceSingleton.WalletInfo;
        IsFetching = false;

        BalanceSingleton.OnBalanceFetch += HandleBalanceFetch;
        TradeStatisticsSingleton.OnTradeStatisticsFetch += HandleTradeStatisticsFetch;

        await base.OnInitializedAsync();
    }

    private async void HandleBalanceFetch(bool isFetching)
    {
        await InvokeAsync(() => {
            Balance = BalanceSingleton.WalletInfo;
            StateHasChanged();
        });
    }

    private async void HandleTradeStatisticsFetch(bool isFetching)
    {
        await InvokeAsync(() => {
            TradeStatistics = TradeStatisticsSingleton.TradeStatistics;
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        BalanceSingleton.OnBalanceFetch -= HandleBalanceFetch;
        TradeStatisticsSingleton.OnTradeStatisticsFetch -= HandleTradeStatisticsFetch;
    }
}
