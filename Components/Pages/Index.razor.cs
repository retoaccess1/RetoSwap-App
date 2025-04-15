using Blazored.LocalStorage;
using Manta.Services;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;

namespace Manta.Components.Pages;

public enum TermuxSetupState
{
    Initial,
    InstallingTermux,
    SettingUpTermux,
    Finished
}

public partial class Index : ComponentBase
{
    public bool IsInitializing { get; set; }
    public TermuxSetupState TermuxSetupState { get; set; }
    public bool IsModalOpen { get; set; }
    public int InstallationStep { get; set; }

    [Inject]
    public ISetupService SetupService { get; set; } = default!;
    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    public void HandleInstallationStepChange(int step)
    {
        InstallationStep = step;
        StateHasChanged();
    }

    public async Task HandleCancelPress()
    {
        await LocalStorage.SetItemAsync("daemon-manual-installation", true);
    }

    public async Task HandleOkPress()
    {
        IsModalOpen = false;

#if ANDROID

        if ((await LocalStorage.GetItemAsync<bool>("termux-installed")) is false)
        {
            try
            {
                TermuxSetupState = TermuxSetupState.InstallingTermux;
                StateHasChanged();

                // TODO prompt first to start this, then system will also have to prompt
                await TermuxInstallService.InstallTermuxAsync();

                await LocalStorage.SetItemAsync("termux-installed", true);
            }
            catch (Exception e)
            {
                // Tell user why install failed TODO
            }

            StateHasChanged();
        }

        if ((await LocalStorage.GetItemAsync<bool>("termux-updated")) is false)
        {
            try
            {
                var accepted = await TermuxSetupService.RequestRequiredPermissions();
                if (accepted)
                {
                    TermuxSetupService.InstallationStep += HandleInstallationStepChange;

                    TermuxSetupState = TermuxSetupState.SettingUpTermux;
                    StateHasChanged();

                    await TermuxSetupService.UpdateTermux();
                    await LocalStorage.SetItemAsync("termux-updated", true);
                }
            }
            catch (Exception e)
            {

            }
            finally
            {
                TermuxSetupService.InstallationStep -= HandleInstallationStepChange;
            }

            TermuxSetupState = TermuxSetupState.Finished;
            StateHasChanged();
        }

        var successfullyStarted = await TermuxSetupService.TryStartHavenoDaemon();
        if (successfullyStarted)
        {
            NavigationManager.NavigateTo("/Market");
        }
        else
        {

        }

#endif
    }

    protected override async Task OnInitializedAsync()
    {
        IsInitializing = true;
        StateHasChanged();

        await SetupService.InitialSetupAsync();

        switch (await SetupService.GetDaemonStatusAsync())
        {
            case DaemonStatus.NOT_INSTALLED:
                // Does user want to manually install?
                var userWantsToInstallManually = await LocalStorage.GetItemAsync<bool>("daemon-manual-installation");
                if (!userWantsToInstallManually)
                {
                    IsModalOpen = true;
                }
                break;
            case DaemonStatus.RUNNING:
                NavigationManager.NavigateTo("/Market");
                break;
            case DaemonStatus.INSTALLED_COULD_NOT_START:
                var a = "Could not start Haveno daemon. Try restarting Termux and Haveno mobile.";
                break;
            default: break;
        }

        IsInitializing = false;

        await base.OnInitializedAsync();
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        return base.OnAfterRenderAsync(firstRender);
    }
}
