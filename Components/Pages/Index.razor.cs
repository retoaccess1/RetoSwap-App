using Blazored.LocalStorage;
using Manta.Models;
using Manta.Services;
using Microsoft.AspNetCore.Components;
using Manta.Singletons;
using Manta.Helpers;
using HavenoSharp.Singletons;
using Grpc.Net.Client.Web;


#if ANDROID
using Manta.Platforms.Android.Services;
#endif

namespace Manta.Components.Pages;

public partial class Index : ComponentBase
{
    public bool IsInitializing { get; set; }
    public TermuxSetupState TermuxSetupState { get; set; }
    public bool IsInstallTypeModalOpen { get; set; }
    public bool IsDaemonIntitializingModalOpen { get; set; }
    public int InstallationStep { get; set; }
    public string TorStartInfo { get; set; } = string.Empty;
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
    public IHavenoDaemonService HavenoDaemonService { get; set; } = default!;
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public GrpcChannelSingleton GrpcChannelSingleton { get; set; } = default!;
    [Inject]
    public DaemonConnectionSingleton DaemonConnectionSingleton { get; set; } = default!;

    public void HandleInstallationStepChange(int step)
    {
        InstallationStep = step;
        StateHasChanged();
    }

    public void HandleTorStartInfoChange(string info)
    {
        TorStartInfo = info;
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
                // Repeat code
                TermuxSetupSingleton.OnTorStartInfo += HandleTorStartInfoChange;
                var successfullyStarted = await TermuxSetupSingleton.TryStartLocalHavenoDaemonAsync(Guid.NewGuid().ToString(), "http://127.0.0.1:3201");
                TermuxSetupSingleton.OnTorStartInfo -= HandleTorStartInfoChange;

                if (!successfullyStarted)
                {
                    // Tell user
                }

                TorStartInfo = string.Empty;
                IsDaemonIntitializingModalOpen = true;
                StateHasChanged();

                CancellationTokenSource initializingTokenSource = new();
                var daemonInitialized = await TermuxSetupSingleton.WaitHavenoDaemonInitializedAsync(initializingTokenSource.Token);
                if (!daemonInitialized)
                {
                    // Tell user
                }

                NavigationManager.NavigateTo("/Market");
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
                            GrpcChannelSingleton.CreateChannel(host, password);
                        }

                        TermuxSetupSingleton.OnTorStartInfo += HandleTorStartInfoChange;
                        var successfullyStarted = await TermuxSetupSingleton.TryStartLocalHavenoDaemonAsync(Guid.NewGuid().ToString(), "http://127.0.0.1:3201");
                        TermuxSetupSingleton.OnTorStartInfo -= HandleTorStartInfoChange;

                        if (!successfullyStarted)
                        {
                            // Tell user
                        }

                        TorStartInfo = string.Empty;
                        IsDaemonIntitializingModalOpen = true;
                        StateHasChanged();

                        CancellationTokenSource initializingTokenSource = new();
                        var daemonInitialized = await TermuxSetupSingleton.WaitHavenoDaemonInitializedAsync(initializingTokenSource.Token);
                        if (!daemonInitialized)
                        {
                            // Tell user
                        }

                        NavigationManager.NavigateTo("/Market");
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
                            GrpcChannelSingleton.CreateChannel(host, password, new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new AndroidSocks5Handler())));

                            if (await TermuxSetupSingleton.IsHavenoDaemonRunningAsync())
                            {
                                NavigationManager.NavigateTo("/Market");
                            }
                            else
                            {
                                // Send param to open modal?
                                NavigationManager.NavigateTo("/Settings");
                            }
                        }
                    }
                    break;
                default: throw new Exception("Invalid DaemonInstallOption");
            }
        }
#elif WINDOWS
        var isInstalled = await HavenoDaemonService.GetIsDaemonInstalledAsync();
        if (isInstalled)
        {
            //await HavenoDaemonService.TryStartLocalHavenoDaemonAsync("", "http://127.0.0.1:3201");

            await SecureStorageHelper.SetAsync("password", "");
            await SecureStorageHelper.SetAsync("host", "http://127.0.0.1:3201");

            GrpcChannelSingleton.CreateChannel("http://127.0.0.1:3201", "");

            NavigationManager.NavigateTo("/Market");
        }
        else
        {

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
