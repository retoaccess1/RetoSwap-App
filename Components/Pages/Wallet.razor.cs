using Blazored.LocalStorage;
using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using Microsoft.AspNetCore.Components;
using System.Globalization;

using static Haveno.Proto.Grpc.Wallets;

namespace Manta.Components.Pages;

public partial class Wallet : ComponentBase, IDisposable
{
    [Inject]
    public BalanceSingleton BalanceSingleton { get; set; } = default!;
    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;

    public WalletInfo? Balance { get; set; }
    public List<string> Addresses { get; set; } = [];
    public List<XmrTx> Transactions { get; set; } = [];

    public decimal PendingFiat { get; set; }
    public decimal AvailableFiat { get; set; }

    public string Memo { get; set; } = string.Empty;
    public string WithdrawalAddress { get; set; } = string.Empty;

    public bool VerifyModalIsOpen { get; set; }
    public bool CreatingTxModalIsOpen { get; set; }

    public XmrTx? Transaction { get; set; }

    private ulong _piconeroAmount;
    public decimal Amount
    {
        get
        {
            return _piconeroAmount.ToMonero();
        }
        set
        {
            _piconeroAmount = value.ToPiconero();
        }
    }

    public int SelectedTabIndex { get; set; }
    public string PreferredCurrency { get; set; } = string.Empty;
    public NumberFormatInfo PreferredCurrencyFormat { get; set; } = default!;
    public bool IsFetching { get; set; }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        return base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        while (true) 
        { 
            try
            {
                PreferredCurrency = await LocalStorage.GetItemAsStringAsync("preferredCurrency") ?? "USD";
                PreferredCurrencyFormat = CurrencyCultureInfo.GetFormatForCurrency((Currency)Enum.Parse(typeof(Currency), PreferredCurrency));

                Balance = BalanceSingleton.WalletInfo;

                if (BalanceSingleton.WalletInfo is not null)
                    Addresses = [BalanceSingleton.WalletInfo.PrimaryAddress];

                PendingFiat = BalanceSingleton.ConvertMoneroToFiat(Balance?.PendingXMRBalance.ToMonero() ?? 0m, PreferredCurrency);
                AvailableFiat = BalanceSingleton.ConvertMoneroToFiat(Balance?.AvailableXMRBalance.ToMonero() ?? 0m, PreferredCurrency);

                BalanceSingleton.OnBalanceFetch += HandleBalanceFetch;

                // Move to long running task
                await GetTransactionsAsync();

                break;
            }
            catch
            {

            }
        
            await Task.Delay(5_000);
        }

        await base.OnInitializedAsync();
    }

    private async Task GetTransactionsAsync()
    {
        using var grpcChannelHelper = new GrpcChannelHelper();

        var walletClient = new WalletsClient(grpcChannelHelper.Channel);
        var xmrTxsResponse = await walletClient.GetXmrTxsAsync(new GetXmrTxsRequest());

        Transactions = [.. xmrTxsResponse.Txs];
    }

    private async void HandleBalanceFetch(bool isFetching)
    {
        await InvokeAsync(() => {
            IsFetching = isFetching;

            Balance = BalanceSingleton.WalletInfo;

            if (BalanceSingleton.WalletInfo is not null)
                Addresses = [BalanceSingleton.WalletInfo.PrimaryAddress];

            PendingFiat = BalanceSingleton.ConvertMoneroToFiat(Balance?.PendingXMRBalance.ToMonero() ?? 0m, PreferredCurrency);
            AvailableFiat = BalanceSingleton.ConvertMoneroToFiat(Balance?.AvailableXMRBalance.ToMonero() ?? 0m, PreferredCurrency);

            StateHasChanged();
        });
    }

    public async Task CreateTransaction()
    {
        if (Balance is null)
            return;

        using var grpcChannelHelper = new GrpcChannelHelper();
        var walletsClient = new WalletsClient(grpcChannelHelper.Channel);

        try
        {
            CreatingTxModalIsOpen = true;

            var request = new CreateXmrTxRequest();
            request.Destinations.Add(new XmrDestination
            {
                Address = WithdrawalAddress,
                // Apparently "1" means max amount
                Amount = _piconeroAmount == Balance.AvailableXMRBalance ? "1" : _piconeroAmount.ToString()
            });

            var response = await walletsClient.CreateXmrTxAsync(request);
            Transaction = response.Tx;

            CreatingTxModalIsOpen = false;
            VerifyModalIsOpen = true;
        }
        catch
        {
            throw;
        }
        finally
        {

        }
    }

    public async Task WithdrawAsync()
    {
        if (Transaction is null)
            return;

        using var grpcChannelHelper = new GrpcChannelHelper();
        var walletsClient = new WalletsClient(grpcChannelHelper.Channel);

        var response = await walletsClient.relayXmrTxAsync(new RelayXmrTxRequest { Metadata = Transaction.Metadata});
    }

    public void Dispose()
    {
        BalanceSingleton.OnBalanceFetch -= HandleBalanceFetch;
    }
}
