using Blazored.LocalStorage;
using CommunityToolkit.Maui.Storage;
using Grpc.Core;
using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;

using static Haveno.Proto.Grpc.Account;

namespace Manta.Components.Pages;

public partial class Settings : ComponentBase, IDisposable
{
    public List<UrlConnection> UrlConnections { get; private set; } = [];
    public bool XMRNodeIsRunning { get; private set; }

    public string Version { get; } = AppInfo.Current.VersionString;
    public string Build { get; } = AppInfo.Current.BuildString;
    public string HavenoVersion { get; private set; } = string.Empty;

    // Languages aren't implemented
    public Dictionary<string, string> Countries { get; set; } = new Dictionary<string, string> { { "English", "English" } };
    public Dictionary<string, string> Currencies { get; set; } = CurrencyCultureInfo.GetCurrencyFullNamesAndCurrencyCodeDictionary().ToDictionary();
    public string PreferredCurrency { get; set; } = string.Empty;

    [Inject]
    public DaemonInfoSingleton DaemonInfoSingleton { get; set; } = default!;
    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;
    [Inject]
    public DaemonConnectionSingleton DaemonConnectionSingleton { get; set; } = default!;
#if ANDROID
    [Inject]
    public TermuxSetupSingleton TermuxSetupSingleton { get; set; } = default!;
#endif
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    public bool IsFetching { get; set; }
    public bool IsBackingUp { get; set; }
    public bool IsConnecting { get; set; }

    public bool IsToggled { get; set; }
    public bool IsWakeLockToggled { get; set; }

    public bool IsConnected { get; set; }

    public bool IsRemoteNodeToggled { get; set; }
    public DaemonInstallOptions DaemonInstallOption { get; set; }

    public string? Password { get; set; }
    public string? Host { get; set; }

    public CancellationTokenSource RemoteNodeConnectCts { get; set; } = new();
    public string? ConnectionError { get; set; }

    public async Task BackupAsync()
    {
        IsBackingUp = true;

        try
        {
            var result = await FolderPicker.Default.PickAsync();
            if (result.IsSuccessful)
            {

            }
            else
            {
                IsBackingUp = false;
                return;
            }

            using var grpcChannelHelper = new GrpcChannelHelper(disableMessageSizeLimit: true);
            var accountClient = new AccountClient(grpcChannelHelper.Channel);

            var backupStream = accountClient.BackupAccount(new BackupAccountRequest());

            using var memoryStream = new MemoryStream();

            while (await backupStream.ResponseStream.MoveNext())
            {
                memoryStream.Write(backupStream.ResponseStream.Current.ZipBytes.Memory.Span);
            }

            var fileSaverResult = await FileSaver.Default.SaveAsync(result.Folder.Path, $"haveno_backup_{DateTime.Now.ToString()}-{Guid.NewGuid()}.zip", memoryStream);
            if (fileSaverResult.IsSuccessful)
            {

            }
            else
            {
                IsBackingUp = false;
                return;
            }
        }
        catch
        {

        }

        IsBackingUp = false;
    }

    public async Task ScanQRCodeAsync()
    {
        await Permissions.RequestAsync<Permissions.Camera>();

        var cameraPage = new CameraPage();

        var current = Application.Current;

        if (current?.MainPage is null)
            throw new Exception("current.MainPage was null");

        await current.MainPage.Navigation.PushModalAsync(cameraPage);

        var scanResults = await cameraPage.WaitForResultAsync();

        var barcode = scanResults.FirstOrDefault();
        if (barcode is not null)
        {
            try
            {
                var split = barcode.Value.Split(';');
                if (split.Length == 2)
                {
                    Host = split[0];
                    Password = split[1];

                    StateHasChanged();
                }
            }
            catch
            {
                throw new Exception("Error parsing QR code");
            }
        }

        await current.MainPage.Navigation.PopModalAsync();
    }

    public async Task CancelConnectToRemoteNode()
    {
        await RemoteNodeConnectCts.CancelAsync();
    }

    public async Task ConnectToRemoteNode()
    {
        // Todo validation
        if (string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(Host))
            return;

        IsConnecting = true;

        await SecureStorageHelper.SetAsync("daemonInstallOption", DaemonInstallOptions.RemoteNode);

        var host = "http://" + Host + ":2134";

        await SecureStorageHelper.SetAsync("password", Password);
        await SecureStorageHelper.SetAsync("host", host);

        GrpcChannelHelper.Password = Password;
        GrpcChannelHelper.Host = host;

#if ANDROID
        await TermuxSetupSingleton.StopLocalHavenoDaemonAsync();
        await TermuxSetupSingleton.CloseTermux();

        try
        {
            // Try for 2 minutes
            for (int i = 0; i < 24; i++)
            {
                if (await TermuxSetupSingleton.IsHavenoDaemonRunningAsync(RemoteNodeConnectCts.Token))
                {
                    IsConnecting = false;
                    await NotificationSingleton.Reset();
                    return;
                }
            }
        }
        catch (TaskCanceledException)
        {
            RemoteNodeConnectCts.Dispose();
            RemoteNodeConnectCts = new();
            return;
        }

        IsConnecting = false;
        ConnectionError = "Could not connect to remote node. Make sure Orbot is installed and configured.";
#endif
    }

    public async Task HandleRemoteNodeToggle(bool isToggled)
    {
        // Prompt that account won't be synced and that if running, local daemon, termux etc needs to be stopped
        if (!isToggled)
        {
#if ANDROID
            // Theres a small issue if orbot is running at the same time as it listens to the same ports that the Termux tor instance listens on, however users should not be regularly switching hosting modes
            await SecureStorageHelper.SetAsync("daemonInstallOption", DaemonInstallOptions.TermuxAutomatic);

            if (await TermuxSetupSingleton.GetIsTermuxAndDaemonInstalledAsync())
            {
                await TermuxSetupSingleton.TryStartLocalHavenoDaemonAsync(Guid.NewGuid().ToString(), "http://127.0.0.1:3201");
            }
            else
            {
                NavigationManager.NavigateTo("/");
            }
#endif

            Password = null;
            Host = null;
        }
    }

    public async Task HandleToggle(bool isToggled)
    {
#if ANDROID
        if (isToggled)
        {
            var status = await Permissions.RequestAsync<NotificationPermission>() == PermissionStatus.Granted;
            if (status)
                await SecureStorageHelper.SetAsync("notifications-enabled", true);
            else 
                IsToggled = false;
        }
        else
        {
            await SecureStorageHelper.SetAsync("notifications-enabled", false);
        }
#endif
    }

    public async Task HandleWakeLockToggle(bool isToggled)
    {
#if ANDROID
        if (!isToggled)
            return;

        if (await TermuxSetupSingleton.RequestEnableWakeLockAsync())
        {
            await SecureStorageHelper.SetAsync("wakelock-enabled", true);
        }
        else
        {
            IsWakeLockToggled = false;
        }
#endif
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private async void HandleDaemonInfoFetch(bool isFetching)
    {
        await InvokeAsync(() => {
            XMRNodeIsRunning = DaemonInfoSingleton.XMRNodeIsRunning;
            UrlConnections = DaemonInfoSingleton.UrlConnections;

            StateHasChanged();
        });
    }

    private async void HandleDaemonConnectionChanged(bool isConnected)
    {
        await InvokeAsync(() => {
            IsConnected = isConnected;
            StateHasChanged();
        });
    }

    protected override async Task OnInitializedAsync()
    {
#if ANDROID
        IsToggled = (await Permissions.CheckStatusAsync<NotificationPermission>() == PermissionStatus.Granted) && (await SecureStorageHelper.GetAsync<bool>("notifications-enabled"));
#endif
        var preferredCurrency = await LocalStorage.GetItemAsStringAsync("preferredCurrency");
        if (preferredCurrency is not null)
        {
            PreferredCurrency = preferredCurrency;
        }
        
        var host = await SecureStorageHelper.GetAsync<string>("host");
        if (host != "http://127.0.0.1:3201")
        {
            Host = host?.Replace("http://", "").Replace(":2134", "");
            Password = await SecureStorageHelper.GetAsync<string>("password");
        }

        DaemonInstallOption = await SecureStorageHelper.GetAsync<DaemonInstallOptions>("daemonInstallOption");
        IsRemoteNodeToggled = DaemonInstallOption == DaemonInstallOptions.RemoteNode;

        IsWakeLockToggled = (await SecureStorageHelper.GetAsync<bool>("wakelock-enabled")) || DaemonInstallOption == DaemonInstallOptions.RemoteNode;

        HavenoVersion = DaemonConnectionSingleton.Version;
        IsConnected = DaemonConnectionSingleton.IsConnected;

        DaemonInfoSingleton.OnDaemonInfoFetch += HandleDaemonInfoFetch;
        DaemonConnectionSingleton.OnConnectionChanged += HandleDaemonConnectionChanged;

        StateHasChanged();

        await base.OnInitializedAsync();
    }

    public async Task HandlePreferredCurrencySubmit(string currencyCode)
    {
        if (string.IsNullOrEmpty(currencyCode))
            return;

        await LocalStorage.SetItemAsStringAsync("preferredCurrency", currencyCode);
        PreferredCurrency = currencyCode;
    }

    public void Dispose()
    {
        DaemonInfoSingleton.OnDaemonInfoFetch -= HandleDaemonInfoFetch;
        DaemonConnectionSingleton.OnConnectionChanged -= HandleDaemonConnectionChanged;
    }
}
