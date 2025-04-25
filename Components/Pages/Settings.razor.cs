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

    public bool IsFetching { get; set; }
    public bool IsBackingUp { get; set; }

    public bool IsToggled { get; set; }

    public bool IsConnected { get; set; }

    public bool IsRemoteNodeToggled { get; set; }
    public DaemonInstallOptions DaemonInstallOption { get; set; }

    public string? Password { get; set; }
    public string? Host { get; set; }

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

    public async Task ConnectToRemoteNode()
    {
        // Todo validation
        if (string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(Host))
            return;

        await SecureStorageHelper.SetAsync("daemonInstallOption", DaemonInstallOptions.RemoteNode);

        var host = "http://" + Host + ":2134";

        await SecureStorageHelper.SetAsync("password", Password);
        await SecureStorageHelper.SetAsync("host", host);

        GrpcChannelHelper.Password = Password;
        GrpcChannelHelper.Host = host;

#if ANDROID
        await TermuxSetupSingleton.StopLocalHavenoDaemonAsync();
#endif
    }

    public async Task HandleRemoteNodeToggle(bool isToggled)
    {
        // Prompt that account won't be synced and that if running, local daemon, termux etc needs to be stopped
        if (!isToggled)
        {
            await SecureStorageHelper.SetAsync("daemonInstallOption", DaemonInstallOptions.TermuxAutomatic);

#if ANDROID
            await TermuxSetupSingleton.TryStartLocalHavenoDaemonAsync(Guid.NewGuid().ToString(), "http://127.0.0.1:3201");
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
        IsToggled = await Permissions.CheckStatusAsync<NotificationPermission>() == PermissionStatus.Granted;
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
