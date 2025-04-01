using Blazored.LocalStorage;
using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;

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

    public bool IsFetching { get; set; }

    public bool IsToggled 
    { 
        get;
        set;
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

    protected override async Task OnInitializedAsync()
    {
#if ANDROID
        IsToggled = await Permissions.CheckStatusAsync<NotificationPermission>() == PermissionStatus.Granted;
#endif

        while (true)
        {
            try
            {
                var preferredCurrency = await LocalStorage.GetItemAsStringAsync("preferredCurrency");
                if (preferredCurrency is not null)
                    PreferredCurrency = preferredCurrency;

                using var grpcChannelHelper = new GrpcChannelHelper();
                var client = new GetVersion.GetVersionClient(grpcChannelHelper.Channel);

                var response = await client.GetVersionAsync(new GetVersionRequest());
                HavenoVersion = response.Version;

                break;
            }
            catch
            {

            }

            await Task.Delay(5_000);
        }

        DaemonInfoSingleton.OnDaemonInfoFetch += HandleDaemonInfoFetch;

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
    }
}
