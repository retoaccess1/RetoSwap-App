using Blazored.LocalStorage;
using Manta.Models;
using Manta.Services;
using Microsoft.AspNetCore.Components;
using Manta.Singletons;
using Manta.Helpers;

namespace Manta.Components.Pages;

public partial class Index : ComponentBase
{
    public bool IsInitializing { get; set; }
    public TermuxSetupState TermuxSetupState { get; set; }
    public bool IsInstallTypeModalOpen { get; set; }
    public int InstallationStep { get; set; }
    public string? InstallationErrorMessage { get; set; }

    [Inject]
    public ISetupService SetupService { get; set; } = default!;
    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
#if ANDROID
    [Inject]
    public TermuxSetupSingleton TermuxSetupSingleton { get; set; } = default!;
#endif
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;

    public void HandleInstallationStepChange(int step)
    {
        InstallationStep = step;
        StateHasChanged();
    }

    public async Task HandleInstall(DaemonInstallOptions daemonInstallOption)
    {
        // Should be able to change from local to remote node and manual to auto. account sync is not a current feature
        await SecureStorageHelper.SetAsync("daemonInstallOption", daemonInstallOption);

        switch (daemonInstallOption)
        {
            case DaemonInstallOptions.TermuxAutomatic:
                await InstallHavenoDaemonAutomatically();
                break;
            case DaemonInstallOptions.TermuxManual:
                NavigationManager.NavigateTo("/Settings");
                break;
            case DaemonInstallOptions.RemoteNode:
                NavigationManager.NavigateTo("/Settings");
                break;
            default: return;
        }
    }

    public async Task InstallHavenoDaemonAutomatically()
    {
        IsInstallTypeModalOpen = false;

#if ANDROID

        if (!await SecureStorageHelper.GetAsync<bool>("termux-installed"))
        {
            try
            {
                TermuxSetupState = TermuxSetupState.InstallingTermux;
                StateHasChanged();

                var res = await TermuxInstallService.InstallTermuxAsync();
                if (!res.Item1)
                {
                    InstallationErrorMessage = res.Item2;
                    return;
                }

                await SecureStorageHelper.SetAsync("termux-installed", true);
            }
            catch (Exception e)
            {
                // Tell user why install failed TODO
            }

            StateHasChanged();
        }

        if (!await SecureStorageHelper.GetAsync<bool>("termux-setup"))
        {
            try
            {
                var accepted = await TermuxSetupSingleton.RequestRequiredPermissionsAsync();
                if (accepted)
                {
                    TermuxSetupSingleton.InstallationStep += HandleInstallationStepChange;

                    TermuxSetupState = TermuxSetupState.SettingUpTermux;
                    StateHasChanged();

                    await TermuxSetupSingleton.SetupTermuxAsync();
                    await SecureStorageHelper.SetAsync("termux-setup", true);

                    TermuxSetupState = TermuxSetupState.Finished;
                    StateHasChanged();

                    var host = "http://127.0.0.1:3201";
                    var password = Guid.NewGuid().ToString();

                    var successfullyStarted = await TermuxSetupSingleton.TryStartLocalHavenoDaemonAsync(password, host);
                    if (successfullyStarted)
                    {
                        NavigationManager.NavigateTo("/Market");
                    }
                    else
                    {

                    }
                }
                else
                {
                    // Permissions required
                    // Maybe show user that installation can't continue
                    await SecureStorageHelper.SetAsync("daemonInstallOption", DaemonInstallOptions.None);

                    TermuxSetupState = TermuxSetupState.Initial;
                    IsInstallTypeModalOpen = true;
                }
            }
            catch (Exception e)
            {

            }
            finally
            {
                TermuxSetupSingleton.InstallationStep -= HandleInstallationStepChange;
            }
        }
#endif
    }

    protected override async Task OnInitializedAsync()
    {
        IsInitializing = true;
        StateHasChanged();

        await SetupService.InitialSetupAsync();

#if ANDROID
        // For first time and if user does not install when first using app
        var daemonInstallOption = await SecureStorageHelper.GetAsync<DaemonInstallOptions>("daemonInstallOption");
        if (daemonInstallOption == DaemonInstallOptions.None)
        {
            IsInstallTypeModalOpen = true;
        }
        else
        {
            switch (daemonInstallOption)
            {
                case DaemonInstallOptions.TermuxAutomatic:
                    var isDaemonInstalled = await TermuxSetupSingleton.GetIsTermuxAndDaemonInstalledAsync();
                    if (!isDaemonInstalled)
                    {
                        // If install failed or partially completed etc
                        IsInstallTypeModalOpen = true;
                    }
                    else
                    {
                        var host = await SecureStorageHelper.GetAsync<string>("host");
                        var password = await SecureStorageHelper.GetAsync<string>("password");

                        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(password))
                        {

                        }
                        else
                        {
                            GrpcChannelHelper.Password = password;
                            GrpcChannelHelper.Host = host;
                        }

                        var successfullyStarted = await TermuxSetupSingleton.TryStartLocalHavenoDaemonAsync(Guid.NewGuid().ToString(), "http://127.0.0.1:3201");
                        if (successfullyStarted)
                        {
                            NavigationManager.NavigateTo("/Market");
                        }
                        else
                        {

                        }
                    }
                    break;
                case DaemonInstallOptions.TermuxManual:
                    NavigationManager.NavigateTo("/Settings");
                    break;
                case DaemonInstallOptions.RemoteNode:
                    {
                        var host = await SecureStorageHelper.GetAsync<string>("host");
                        var password = await SecureStorageHelper.GetAsync<string>("password");

                        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(password))
                        {
                            NavigationManager.NavigateTo("/Settings");
                        }
                        else
                        {
                            GrpcChannelHelper.Password = password;
                            GrpcChannelHelper.Host = host;

                            if (await TermuxSetupSingleton.IsHavenoDaemonRunningAsync())
                            {
                                NavigationManager.NavigateTo("/Market");
                            }
                            else
                            {
                                NavigationManager.NavigateTo("/Settings");
                            }
                        }
                    }
                    break;
                default: throw new Exception("Invalid DaemonInstallOption");
            }
        }
#endif

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
