using Blazored.LocalStorage;
using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Models;
using Manta.Services;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using static Haveno.Proto.Grpc.GetVersion;

namespace Manta.Components.Pages;

public partial class Market : ComponentBase, IDisposable
{
    public string Version { get; set; } = string.Empty;
    public WalletInfo? Balance { get; set; }
    public bool IsFetching { get; set; }

    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public IJSRuntime JS { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;

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
            catch
            {

            }

            await Task.Delay(5_000);
        }

        Balance = BalanceSingleton.LastBalance;
        IsFetching = false;

        StateHasChanged();

        BalanceSingleton.OnBalanceFetch += HandleBalanceFetch;

        await base.OnInitializedAsync();
    }

    // ASYNC VOID!
    private async void HandleBalanceFetch(bool isFetching)
    {
        await InvokeAsync(() => {
            Balance = BalanceSingleton.LastBalance;
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        BalanceSingleton.OnBalanceFetch -= HandleBalanceFetch;
    }
}
