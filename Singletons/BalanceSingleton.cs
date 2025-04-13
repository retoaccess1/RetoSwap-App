using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Models;

using static Haveno.Proto.Grpc.Price;
using static Haveno.Proto.Grpc.Wallets;

namespace Manta.Singletons;

public class BalanceSingleton
{
    public WalletInfo? WalletInfo { get; private set; }
    public List<MarketPriceInfo> MarketPriceInfos { get; private set; } = [];
    public Dictionary<string, decimal> MarketPriceInfoDictionary { get; set; } = [];

    public event Action<bool>? OnBalanceFetch;

    public BalanceSingleton()
    {
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
                OnBalanceFetch?.Invoke(true);

                using var grpcChannelHelper = new GrpcChannelHelper();

                var walletClient = new WalletsClient(grpcChannelHelper.Channel);
                var balanceResponse = await walletClient.GetBalancesAsync(new GetBalancesRequest());
                var primaryAddressResponse = await walletClient.GetXmrPrimaryAddressAsync(new GetXmrPrimaryAddressRequest());

                WalletInfo = new WalletInfo
                {
                    AvailableXMRBalance = balanceResponse.Balances.Xmr.AvailableBalance,
                    XMRBalance = balanceResponse.Balances.Xmr.Balance,
                    PrimaryAddress = primaryAddressResponse.PrimaryAddress,
                    PendingXMRBalance = balanceResponse.Balances.Xmr.Balance - balanceResponse.Balances.Xmr.AvailableBalance,
                    ReservedTradeBalance = balanceResponse.Balances.Xmr.ReservedTradeBalance,
                    ReservedOfferBalance = balanceResponse.Balances.Xmr.ReservedOfferBalance
                };

                Console.WriteLine("Finished fetching balance");

                var priceClient = new PriceClient(grpcChannelHelper.Channel);
                var getPricesResponse = await priceClient.GetMarketPricesAsync(new MarketPricesRequest());
                
                MarketPriceInfos = [.. getPricesResponse.MarketPrice];
                MarketPriceInfoDictionary = MarketPriceInfos.ToDictionary(x => x.CurrencyCode, x => (decimal)x.Price);

                Console.WriteLine("Finished fetching prices");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                OnBalanceFetch?.Invoke(false);
            }

            await Task.Delay(5_000);
        }
    }
}
