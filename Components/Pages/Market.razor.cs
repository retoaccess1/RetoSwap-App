using Blazored.LocalStorage;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Manta.Components.Pages;

public partial class Market : ComponentBase, IDisposable
{
    private int _index = -1;

    private AxisChartOptions _axisChartOptions = new() 
    { 
        MatchBoundsToSize = true 
    };

    private ChartOptions _options = new()
    {
        YAxisLines = false,
        YAxisTicks = 1,
        MaxNumYAxisTicks = 10,
        YAxisRequireZeroPoint = true,
        XAxisLines = false,
        LineStrokeWidth = 2,
        ShowLabels = false,
        ShowLegend = false,
        ShowLegendLabels = false,
        ShowToolTips = false,
        YAxisLabelPosition = YAxisLabelPosition.Left,
        ChartPalette = ["#bee8c2"]
    };

    private List<ChartSeries> _series = [];

    private string[] _xAxisLabels = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    public string Interval { get; set; } = "Year";

    public int Year
    { 
        get;
        set 
        { 
            field = value; 
            ProcessTradeStatistics(); 
        } 
    }

    public List<int> Years { get; set; } = [];

    public bool IsFetching { get; set; }
    public string Version { get; set; } = string.Empty;
    public WalletInfo? WalletInfo { get; set; }
    public List<TradeStatistic> TradeStatistics { get; private set; } = [];

    public string PreferredCurrency { get; set; } = string.Empty;
    public string CurrentMarketPrice { get; set; } = string.Empty;

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

                await BalanceSingleton.InitializedTCS.Task;

                PreferredCurrency = await LocalStorage.GetItemAsStringAsync("preferredCurrency") ?? "USD";
                CurrentMarketPrice = BalanceSingleton.MarketPriceInfoDictionary[PreferredCurrency].ToString("0.00");

                break;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            await Task.Delay(5_000);
        }

        TradeStatistics = TradeStatisticsSingleton.TradeStatistics;
        ProcessTradeStatistics();

        WalletInfo = BalanceSingleton.WalletInfo;
        IsFetching = false;

        BalanceSingleton.OnBalanceFetch += HandleBalanceFetch;
        TradeStatisticsSingleton.OnTradeStatisticsFetch += HandleTradeStatisticsFetch;

        await base.OnInitializedAsync();
    }

    private async void HandleBalanceFetch(bool isFetching)
    {
        await InvokeAsync(() => {
            WalletInfo = BalanceSingleton.WalletInfo;
            StateHasChanged();
        });
    }

    private async void HandleTradeStatisticsFetch(bool isFetching)
    {
        await InvokeAsync(() => {
            TradeStatistics = TradeStatisticsSingleton.TradeStatistics;
            ProcessTradeStatistics();

            StateHasChanged();
        });
    }

    // UpdateChart
    public void ProcessTradeStatistics()
    {
        if (Years.Count == 0) 
        {
            Years = TradeStatistics
                .OrderBy(x => x.Date)
                .GroupBy(x => x.Date.Year)
                .Select(x => x.Key)
                .ToList();

            Year = Years.FirstOrDefault();
        }

        var volumePerMonth = new double[12];

        for (int i = 0; i < volumePerMonth.Length; i++)
        {
            volumePerMonth[i] = (double)TradeStatistics
                .Where(x => x.Date.Month == i + 1 && x.Date.Year == Year)
                .Aggregate(0m, (x, y) => x + ((ulong)y.Amount).ToMonero());
        }

        _series = new List<ChartSeries>()
        {
            new ChartSeries() { Name = "Volume in XMR", Data = volumePerMonth },
        };
    }

    public void Dispose()
    {
        BalanceSingleton.OnBalanceFetch -= HandleBalanceFetch;
        TradeStatisticsSingleton.OnTradeStatisticsFetch -= HandleTradeStatisticsFetch;
    }
}
