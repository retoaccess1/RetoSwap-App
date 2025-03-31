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
    public bool IsInstallingTermux { get; set; }
    public bool IsSettingUpTermux { get; set; }
    public bool IsModalOpen { get; set; }

    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public IJSRuntime JS { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;

    public async Task HandleOkPress()
    {
#if ANDROID

        if ((await LocalStorage.GetItemAsync<bool>("termux-installed")) is false)
        {
            try
            {
                IsInstallingTermux = true;
                StateHasChanged();

                // TODO prompt first to start this, then system will also have to prompt
                await TermuxInstallService.InstallTermuxAsync();

                await LocalStorage.SetItemAsync("termux-installed", true);
            }
            catch (Exception e)
            {
                // Tell user why install failed TODO
            }

            IsInstallingTermux = false;
            StateHasChanged();
        }

        if ((await LocalStorage.GetItemAsync<bool>("termux-updated")) is false)
        {
            try
            {
                var accepted = await TermuxSetupService.RequestRequiredPermissions();
                if (accepted)
                {
                    IsSettingUpTermux = true;
                    StateHasChanged();

                    await TermuxSetupService.UpdateTermux();
                    await LocalStorage.SetItemAsync("termux-updated", true);
                }
            }
            catch (Exception e)
            {

            }

            IsSettingUpTermux = false;
            IsModalOpen = false;
            StateHasChanged();
        }

        _ = TermuxSetupService.StartHavenoDaemon();

#endif
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
        IsModalOpen = !((await LocalStorage.GetItemAsync<bool>("termux-installed")) && (await LocalStorage.GetItemAsync<bool>("termux-updated")));
        if (!IsModalOpen && TermuxSetupService.HavenoDaemonTask is null)
        {
#if ANDROID
            _ = TermuxSetupService.StartHavenoDaemon();
#endif
        }

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

    }
}
