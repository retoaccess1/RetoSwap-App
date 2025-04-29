using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using Protobuf;

using static Haveno.Proto.Grpc.Offers;
using static Haveno.Proto.Grpc.PaymentAccounts;

namespace Manta.Components.Pages;

public class AdjustedPrices
{
    public decimal FiatPrice;
    public decimal MoneroAmount;
}

public partial class CreateOffer : ComponentBase
{
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;

    [Parameter]
    public string Direction { get; set; } = string.Empty;
    [Parameter]
    public EventCallback OnCloseCreateOffer { get; set; }
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    // Repeat
    public Dictionary<string, string> CurrencyCodes { get; set; } = [];
    public string SelectedPaymentAccountId 
    { 
        get; 
        set 
        { 
            field = value;

            if (!string.IsNullOrEmpty(value))
            {
                CurrencyCodes = ProtoPaymentAccounts.FirstOrDefault(x => x.Id == field)?.TradeCurrencies.ToDictionary(x => x.Code, x => $"{x.Name} ({x.Code})") ?? [];
                SelectedCurrencyCode = ProtoPaymentAccounts.FirstOrDefault(x => x.Id == SelectedPaymentAccountId)?.TradeCurrencies.FirstOrDefault()?.Code ?? throw new Exception();

                // Get trade limits
                var paymentMethod = ProtoPaymentAccounts.First(x => x.Id == field).PaymentMethod;
                MaxTradeLimit = ((ulong)paymentMethod.MaxTradeLimit).ToMonero();
            }
            else
            {
                CurrencyCodes = [];
                // Doesnt really clear as the dropdown does not re-render TODO
                SelectedCurrencyCode = "";
            }

            Clear();

            StateHasChanged();
        } 
    } = string.Empty;

    public List<PaymentAccount> ProtoPaymentAccounts { get; set; } = [];
    public Dictionary<string, string> PaymentAccounts { get; set; } = [];

    public string SelectedCurrencyCode
    {
        get;
        set
        {
            field = value;
            Clear();
        }
    } = string.Empty;

    // Currently does not respect trade limits, TODO
    public decimal MaxTradeLimit { get; set; }
    public decimal MoneroAmount { get; set; }
    public decimal MinimumMoneroAmount { get; set; }
    public decimal MarketPriceMarginPct { get; set; }
    public decimal FiatPrice { get; set; }
    public decimal FixedPrice { get; set; }
    public bool UseFixedPrice { get; set; }
    public decimal TriggerAmount { get; set; }
    public decimal SecurityDepositPct { get; set; } = 15m;
    public bool BuyerAsTakerWithoutDeposit 
    { 
        get;
        set
        {
            field = value;
            SecurityDepositPct = 15m;
        }
    }

    public bool IsFetching { get; set; }

    public void Clear()
    {
        MinimumMoneroAmount = 0m;
        MoneroAmount = 0m;
        FiatPrice = 0m;
        MarketPriceMarginPct = 0m;
        TriggerAmount = 0;
        SecurityDepositPct = 15m;
    }

    // Gives the adjusted amounts so that the fiat value is a whole number
    public AdjustedPrices GetAdjustedPrices(decimal unadjustedMoneroAmount, decimal priceForOneXMR, decimal marketPriceMarginPct)
    {
        decimal adjustedMktPrice;
        var percent = marketPriceMarginPct / 100m;

        if (Direction == "BUY")
        {
            adjustedMktPrice = priceForOneXMR - (priceForOneXMR * percent);
        }
        else
        {
            adjustedMktPrice = priceForOneXMR + (priceForOneXMR * percent);
        }

        var adjustedPrices = new AdjustedPrices
        {
            FiatPrice = Math.Round(adjustedMktPrice * unadjustedMoneroAmount),
        };

        if (adjustedMktPrice != 0m)
        {
            adjustedPrices.MoneroAmount = Math.Round(adjustedPrices.FiatPrice / adjustedMktPrice, 4);
        }
        else
        {
            adjustedPrices.MoneroAmount = 0m;
        }

        return adjustedPrices;
    }

    public void Calculate(decimal value, string inputName)
    {
        BalanceSingleton.MarketPriceInfoDictionary.TryGetValue(SelectedCurrencyCode, out var priceForOneXMR);

        switch (inputName)
        {
            case "MoneroAmount":
                {
                    var adjustedPrices = GetAdjustedPrices(value, priceForOneXMR, MarketPriceMarginPct);

                    MoneroAmount = adjustedPrices.MoneroAmount;
                    FiatPrice = adjustedPrices.FiatPrice;

                    if (MoneroAmount < MinimumMoneroAmount || MinimumMoneroAmount == 0m)
                    {
                        MinimumMoneroAmount = MoneroAmount;
                    }
                }
                break;
            case "MinimumMoneroAmount":
                {
                    var adjustedPrices = GetAdjustedPrices(value, priceForOneXMR, MarketPriceMarginPct);

                    MinimumMoneroAmount = adjustedPrices.MoneroAmount;

                    if (MinimumMoneroAmount > MoneroAmount)
                    {
                        MoneroAmount = MinimumMoneroAmount;
                    }
                }
                break;
            case "FixedPrice":
                {
                    if (!UseFixedPrice)
                        return;

                    if (value == 0)
                        return;

                    var percent = value / priceForOneXMR;

                    MarketPriceMarginPct = Math.Round(percent - 1m, 2) * 100m;

                    FiatPrice = Math.Round(MoneroAmount * value);

                    if (MoneroAmount == MinimumMoneroAmount)
                    {
                        MoneroAmount = Math.Round(FiatPrice / value, 4);
                        MinimumMoneroAmount = MoneroAmount;
                    }
                    else
                    {
                        MoneroAmount = Math.Round(FiatPrice / value, 4);

                        if (MoneroAmount < MinimumMoneroAmount || MinimumMoneroAmount == 0m)
                        {
                            MinimumMoneroAmount = MoneroAmount;
                        }
                    }
                }
                break;
            case "MarketPriceMarginPct":
                {
                    if (UseFixedPrice)
                        return;

                    if (value >= 100m)
                        value = 99.99m;

                    decimal adjustedMktPrice;
                    var percent = MarketPriceMarginPct / 100m;

                    if (Direction == "BUY")
                    {
                        adjustedMktPrice = priceForOneXMR - (priceForOneXMR * percent);
                    }
                    else
                    {
                        adjustedMktPrice = priceForOneXMR + (priceForOneXMR * percent);
                    }

                    FixedPrice = adjustedMktPrice;
                    FiatPrice = Math.Round(adjustedMktPrice * MoneroAmount);

                    if (MoneroAmount == MinimumMoneroAmount)
                    {
                        MoneroAmount = Math.Round(FiatPrice / adjustedMktPrice, 4);
                        MinimumMoneroAmount = MoneroAmount;
                    }
                    else
                    {
                        MoneroAmount = Math.Round(FiatPrice / adjustedMktPrice, 4);

                        if (MoneroAmount < MinimumMoneroAmount || MinimumMoneroAmount == 0m)
                        {
                            MinimumMoneroAmount = MoneroAmount;
                        }
                    }
                }
                break;
            case "FiatPrice":
                {
                    var fiatPrice = Math.Round(FiatPrice);

                    decimal adjustedMktPrice;
                    var percent = MarketPriceMarginPct / 100m;

                    if (Direction == "BUY")
                    {
                        adjustedMktPrice = priceForOneXMR - (priceForOneXMR * percent);
                    }
                    else
                    {
                        adjustedMktPrice = priceForOneXMR + (priceForOneXMR * percent);
                    }

                    if (MoneroAmount == MinimumMoneroAmount)
                    {
                        MoneroAmount = Math.Round(fiatPrice / adjustedMktPrice, 4);
                        MinimumMoneroAmount = MoneroAmount;
                    }
                    else
                    {
                        MoneroAmount = Math.Round(fiatPrice / adjustedMktPrice, 4);

                        if (MoneroAmount < MinimumMoneroAmount || MinimumMoneroAmount == 0m)
                        {
                            MinimumMoneroAmount = MoneroAmount;
                        }
                    }
                }
                break;
            default: break;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        using var grpcChannelHelper = new GrpcChannelHelper();
        var paymentAccountsClient = new PaymentAccountsClient(grpcChannelHelper.Channel);

        var paymentAccountResponse = await paymentAccountsClient.GetPaymentAccountsAsync(new GetPaymentAccountsRequest());
        PaymentAccounts = paymentAccountResponse.PaymentAccounts.ToDictionary(x => x.Id, x => x.AccountName);
        ProtoPaymentAccounts = paymentAccountResponse.PaymentAccounts.ToList();
        
        if (ProtoPaymentAccounts.Count != 0)
        {
            SelectedPaymentAccountId = PaymentAccounts.Select(x => x.Key).FirstOrDefault() ?? string.Empty;
            SelectedCurrencyCode = paymentAccountResponse.PaymentAccounts.FirstOrDefault(x => x.Id == SelectedPaymentAccountId)?.TradeCurrencies.FirstOrDefault()?.Code ?? throw new Exception();

            // Get trade limits
            var paymentMethod = paymentAccountResponse.PaymentAccounts.First(x => x.Id == SelectedPaymentAccountId).PaymentMethod;
            MaxTradeLimit = ((ulong)paymentMethod.MaxTradeLimit).ToMonero();
        }

        await base.OnInitializedAsync();
    }

    public async Task PostOfferAsync()
    {
        try
        {
            IsFetching = true;

            using var channelHelper = new GrpcChannelHelper();
            var offersClient = new OffersClient(channelHelper.Channel);

            var request = new PostOfferRequest
            {
                Amount = MoneroAmount.ToPiconero(),
                MinAmount = MinimumMoneroAmount.ToPiconero(),
                PaymentAccountId = SelectedPaymentAccountId,
                CurrencyCode = SelectedCurrencyCode,
                Direction = Direction,
                SecurityDepositPct = (double)(SecurityDepositPct / 100m),

                TriggerPrice = TriggerAmount.ToString(),
                MarketPriceMarginPct = 0,
                Price = "",
                UseMarketBasedPrice = true,
                BuyerAsTakerWithoutDeposit = BuyerAsTakerWithoutDeposit
            };

            if (UseFixedPrice)
            {
                request.Price = FixedPrice.ToString();
                request.UseMarketBasedPrice = false;
            }
            else
            {
                if (MarketPriceMarginPct == 0m)
                {
                    request.MarketPriceMarginPct = 0;
                }
                else 
                {
                    request.MarketPriceMarginPct = (double)MarketPriceMarginPct;
                }
            }

            var response = await offersClient.PostOfferAsync(request);

            NavigationManager.NavigateTo("/myoffers?title=My%20Offers");
        }
        catch
        {
            throw;
        }
        finally
        {
            IsFetching = false;
        }
    }
}
