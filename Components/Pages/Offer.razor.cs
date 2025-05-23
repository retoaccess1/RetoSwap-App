using HavenoSharp.Models;
using HavenoSharp.Models.Requests;
using HavenoSharp.Services;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using System.Linq;

namespace Manta.Components.Pages;

public partial class Offer : ComponentBase, IDisposable
{
    [Parameter]
    [SupplyParameterFromQuery]
    public string OfferId { get; set; } = string.Empty;
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;
    [Inject]
    public NotificationSingleton NotificationSingleton { get; set; } = default!;
    [Inject]
    public IHavenoOfferService OfferService { get; set; } = default!;
    [Inject]
    public IHavenoPaymentAccountService PaymentAccountService { get; set; } = default!;
    [Inject]
    public IHavenoTradeService TradeService { get; set; } = default!;

    public OfferInfo? OfferInfo { get; set; }

    private ulong _piconeroAmount;
    public decimal Amount 
    {
        get; 
        set 
        {
            if (value > OfferInfo!.Amount.ToMonero())
            {
                field = OfferInfo.Amount.ToMonero();
            }
            else if (value < OfferInfo.MinAmount.ToMonero())
            {
                field = OfferInfo.MinAmount.ToMonero();
            }
            else
            {
                field = value; 
            }

            _piconeroAmount = field.ToPiconero();

            if (OfferInfo is not null)  // Price changes...
                FiatAmount = decimal.Parse(OfferInfo.Price) * Amount;
        } 
    }

    public CancellationTokenSource CancelOfferCts { get; set; } = new();

    public decimal FiatAmount { get { return Math.Round(field, 0); } set { field = value; } }
    public string SelectedPaymentAccountId { get; set; } = string.Empty;
    public Dictionary<string, string> PaymentAccounts { get; set; } = [];

    public bool IsTakingOffer { get; set; }
    public bool UserDoesNotHaveAccount { get; set; }
    public string? AccountToCreate { get; set; }
    public string? AccountErrorMessage { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            OfferInfo = await OfferService.GetOfferAsync(OfferId);

            FiatAmount = decimal.Parse(OfferInfo.Price) * Amount;
            Amount = OfferInfo.Amount.ToMonero();

            var paymentAccounts = await PaymentAccountService.GetPaymentAccountsAsync();

            List<PaymentAccount> sameTypePaymentAccounts = [];

            sameTypePaymentAccounts = paymentAccounts.Where(x => x.PaymentMethod.Id == OfferInfo.PaymentMethodId).ToList();

            if (sameTypePaymentAccounts.Count == 0)
            {
                UserDoesNotHaveAccount = true;
                AccountToCreate = OfferInfo.PaymentMethodId;
                AccountErrorMessage = $"You do not have an account of this type ({OfferInfo.PaymentMethodShortName}).";
                return;
            }

            var currencyCode = OfferInfo.PaymentMethodId == "BLOCK_CHAINS" ? OfferInfo.BaseCurrencyCode : OfferInfo.CounterCurrencyCode;

            sameTypePaymentAccounts = sameTypePaymentAccounts
                .Where(x => x.TradeCurrencies.Select(x => x.Code).Contains(currencyCode)).ToList();

            SelectedPaymentAccountId = sameTypePaymentAccounts
                .FirstOrDefault(x => x.PaymentMethod.Id == OfferInfo.PaymentMethodId)?.Id ?? string.Empty;

            if (string.IsNullOrEmpty(SelectedPaymentAccountId))
            {
                UserDoesNotHaveAccount = true;
                AccountErrorMessage = $"You do not have a {OfferInfo.PaymentMethodShortName} account that supports this currency ({currencyCode}).";
                AccountToCreate = OfferInfo.PaymentMethodId;
                return;
            }

            PaymentAccounts = sameTypePaymentAccounts.ToDictionary(x => x.Id, x => x.AccountName);
        }
        catch
        {
            Cancel();
        }

        await base.OnInitializedAsync();
    }

    public async Task TakeOfferAsync()
    {
        IsTakingOffer = true;

        try
        {
            var takeOfferRequest = new TakeOfferRequest
            {
                OfferId = OfferInfo?.Id,
                Amount = _piconeroAmount,
                PaymentAccountId = SelectedPaymentAccountId,
                Challenge = OfferInfo?.Challenge
            };

            var response = await TradeService.TakeOfferAsync(takeOfferRequest);

            NotificationSingleton.TradeInfos.TryAdd(response.Trade.TradeId, response.Trade);

            NavigationManager.NavigateTo("Trades");
        }
        catch (OperationCanceledException e)
        {
            // Show user it was canceled
        }
        catch (Exception e)
        {
            throw;
        }
        finally
        {
            IsTakingOffer = false;
        }
    }

    public void NavigateToAccount()
    {
        NavigationManager.NavigateTo($"account?accountToCreate={AccountToCreate}&title=Account");
    }

    public void Cancel()
    {
        NavigationManager.NavigateTo("buysell?title=Buy%20%26%20Sell");
    }

    public void Dispose()
    {

    }
}
