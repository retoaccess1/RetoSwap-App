using HavenoSharp.Models;
using HavenoSharp.Services;
using Manta.Helpers;
using Manta.Models;
using Manta.Services;

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
#if DEBUG
        // Testing how long task stays alive
        Task.Run(Poll);
#endif
    }

    private async Task Poll()
    {
        INotificationManagerService? notifService = null;
        int delay = 10_000;

        while (true)
        {
            try
            {
                notifService = IPlatformApplication.Current?.Services.GetService<INotificationManagerService>();
                if (notifService is null)
                    throw new Exception("notifService is null");

                var offerService = IPlatformApplication.Current?.Services.GetService<IHavenoOfferService>();
                if (offerService is null)
                    throw new Exception("offerService is null");

                var offers = await offerService.GetOffersAsync("", "BUY");

                notifService.SendNotification(DateTime.Now.ToString(), $"Offer count: {offers.Count}");

                delay = 300_000;
            }
            catch (Exception e)
            {
                notifService?.SendNotification(DateTime.Now.ToString(), $"Exception: {e}");
            }

            await Task.Delay(delay);
        }
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
