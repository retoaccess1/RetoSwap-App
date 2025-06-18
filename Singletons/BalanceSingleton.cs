using HavenoSharp.Models;
using HavenoSharp.Services;
using Manta.Models;

namespace Manta.Singletons;

public class BalanceSingleton
{
    private readonly IServiceProvider _serviceProvider;

    public WalletInfo? WalletInfo { get; private set; }
    public List<MarketPriceInfo> MarketPriceInfos { get; private set; } = [];
    public Dictionary<string, decimal> MarketPriceInfoDictionary { get; set; } = [];
    public TaskCompletionSource<bool> InitializedTCS { get; private set; } = new();

    public event Action<bool>? OnBalanceFetch;

    public BalanceSingleton(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Task.Run(PollBalance);
    }

    public decimal ConvertMoneroToFiat(decimal moneroAmount, string currencyCode)
    {
        try
        {
            var oneXmrInFiat = MarketPriceInfoDictionary[currencyCode];
            return oneXmrInFiat * moneroAmount;
        }
        catch
        {
            return 0m;
        }
    }

    private async Task PollBalance()
    {
        while (true)
        {
            try
            {
                await PauseTokenSource.WaitWhilePausedAsync();

                OnBalanceFetch?.Invoke(true);

                using var scope = _serviceProvider.CreateScope();
                var walletService = _serviceProvider.GetRequiredService<IHavenoWalletService>();
                var priceService = _serviceProvider.GetRequiredService<IHavenoPriceService>();

                var balances = await walletService.GetBalancesAsync();
                var primaryAddress = await walletService.GetXmrPrimaryAddressAsync();

                WalletInfo = new WalletInfo
                {
                    AvailableXMRBalance = balances.AvailableXMRBalance,
                    XMRBalance = balances.XMRBalance,
                    PrimaryAddress = primaryAddress,
                    PendingXMRBalance = balances.XMRBalance - balances.AvailableXMRBalance,
                    ReservedTradeBalance = balances.ReservedTradeBalance,
                    ReservedOfferBalance = balances.ReservedOfferBalance
                };

                Console.WriteLine("Finished fetching balance");

                MarketPriceInfos = await priceService.GetMarketPricesAsync();
                MarketPriceInfoDictionary = MarketPriceInfos.ToDictionary(x => x.CurrencyCode, x => (decimal)x.Price);

                Console.WriteLine("Finished fetching prices");

                if (!InitializedTCS.Task.IsCompleted)
                    InitializedTCS.SetResult(true);
            }
            catch (Exception)
            {
                //Console.WriteLine(e);
            }
            finally
            {
                OnBalanceFetch?.Invoke(false);
            }

            await Task.Delay(5_000);
        }
    }
}
