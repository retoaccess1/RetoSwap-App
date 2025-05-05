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
    public double ProgressPercentage { get; set; }
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
            case DaemonInstallOptions.RemoteNode:
                NavigationManager.NavigateTo("/Settings");
                break;
            default: return;
        }
    }

    public void HandleProgressPercentageChange(double progressPercentage)
    {
        ProgressPercentage = progressPercentage;
        StateHasChanged();
    }

    public async Task InstallHavenoDaemonAutomatically()
    {
        IsInstallTypeModalOpen = false;

#if ANDROID

        if (!await SecureStorageHelper.GetAsync<bool>("termux-installed"))
        {
            TermuxSetupState = TermuxSetupState.InstallingTermux;
            StateHasChanged();

            //TermuxInstallService.OnProgressPercentageChange += HandleProgressPercentageChange;

            var res = await TermuxInstallService.InstallTermuxAsync();
            if (!res.Item1)
            {
                InstallationErrorMessage = res.Item2;
                StateHasChanged();
                return;
            }

            //TermuxInstallService.OnProgressPercentageChange -= HandleProgressPercentageChange;

            TermuxSetupState = TermuxSetupState.SettingUpTermux;
            StateHasChanged();

            await TermuxSetupSingleton.OpenTermux();

            // Add timeout
            await TermuxReceiver.TaskCompletionSource.Task;

            TermuxSetupState = TermuxSetupState.Finished;
            StateHasChanged();

            await SecureStorageHelper.SetAsync("termux-installed", true);

            var accepted = await TermuxPermissionHelper.RequestRunCommandPermissionAsync();
            if (accepted)
            {
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
                await SecureStorageHelper.SetAsync("daemonInstallOption", DaemonInstallOptions.None);

                TermuxSetupState = TermuxSetupState.Initial;
                IsInstallTypeModalOpen = true;
            }

            StateHasChanged();
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
