using Haveno.Proto.Grpc;
using Manta.Helpers;
using Manta.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Protobuf;

using static Haveno.Proto.Grpc.PaymentAccounts;

namespace Manta.Components.Pages;

public partial class Account : ComponentBase
{
    private ValidationMessageStore? _messageStore;
    private EditContext? _editContext;

    public PaymentAccountForm? PaymentAccountForm { get; set; }
    public CreateCryptoCurrencyPaymentAccountRequest? CreateCryptoCurrencyPaymentAccountRequest { get; set; }
    public PaymentAccount? SelectedPaymentAccount { get; set; }

    public Dictionary<string, string> TraditionalPaymentMethodStrings { get; set; } = [];
    public Dictionary<string, string> CryptoPaymentMethodStrings { get; set; } = [];
    public Dictionary<string, string> VisiblePaymentMethodStrings { get; set; } = [];
    public List<SelectedCurrency> SupportedCurrencyCodes { get; set; } = [];
    public List<PaymentAccount> PaymentAccounts { get; set; } = [];
    public List<PaymentAccount> CryptoPaymentAccounts { get; set; } = [];
    public List<PaymentMethod> TraditionalPaymentMethods { get; set; } = [];
    public List<PaymentMethod> CryptoPaymentMethods { get; set; } = [];

    public bool CustomAccountNameEnabled { get; set; }

    public string? AccountToDelete { get; set; }
    public bool ShowDeleteAccountModal 
    { 
        get; 
        set 
        { 
            field = value;
            if (!field)
            {
                AccountToDelete = null;
            }
        } 
    }

    public int SelectedTabIndex 
    { 
        get;
        set
        {
            field = value;
            switch (field)
            {
                case 0:
                    VisiblePaymentMethodStrings = TraditionalPaymentMethodStrings;
                    break;
                case 1:
                    VisiblePaymentMethodStrings = CryptoPaymentMethodStrings;
                    break;
                default: break;
            }
        }
    }

    public string SelectedPaymentMethodId 
    { 
        get; 
        set 
        { 
            field = value;
            SelectedPaymentAccount = null;
            CustomAccountNameEnabled = false;

            if (string.IsNullOrEmpty(value))
            {
                SupportedCurrencyCodes = [];
                PaymentAccountForm = default!;
                return;
            }

            if (field == "BLOCK_CHAINS")
            {
                CreateCryptoCurrencyPaymentAccountRequest = new();
            }

            SupportedCurrencyCodes = TraditionalPaymentMethods
                .FirstOrDefault(x => x.Id == SelectedPaymentMethodId)!
                .SupportedAssetCodes.Select(x => new SelectedCurrency { Code = x, IsSelected = false })
                .ToList();

            using var payChannelHelper = new GrpcChannelHelper();
            var paymentAccountsClient = new PaymentAccountsClient(payChannelHelper.Channel);

            // Ehhh...
            var getPaymentAccountFormReply = paymentAccountsClient.GetPaymentAccountForm(new GetPaymentAccountFormRequest
            {
                PaymentMethodId = SelectedPaymentMethodId
            });

            // Add a default account name
            var accountName = getPaymentAccountFormReply.PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == PaymentAccountFormField.Types.FieldId.AccountName);
            if (accountName is not null)
            {
                getPaymentAccountFormReply.PaymentAccountForm.Fields.Remove(accountName);

                var newAccountName = new PaymentAccountFormField
                {
                    Id = PaymentAccountFormField.Types.FieldId.AccountName,
                    Label = accountName.Label,
                    Value = SelectedPaymentMethodId
                };

                getPaymentAccountFormReply.PaymentAccountForm.Fields.Add(newAccountName);
            }

            PaymentAccountForm = getPaymentAccountFormReply.PaymentAccountForm;

            _editContext = new EditContext(PaymentAccountForm);
            _messageStore = new ValidationMessageStore(_editContext);
            _editContext.OnFieldChanged += ValidateModel;
        }
    } = string.Empty;

    public bool IsFetching { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        // Might be worth switching these to for with a max of 3 attempts...
        while (true)
        {
            try
            {
                using var paymentAccountChannel = new GrpcChannelHelper();
                var paymentAccountsClient = new PaymentAccountsClient(paymentAccountChannel.Channel);

                var paymentAccounts = await paymentAccountsClient.GetPaymentAccountsAsync(new GetPaymentAccountsRequest());
                PaymentAccounts = [.. paymentAccounts.PaymentAccounts];

                var paymentMethodsResponse = await paymentAccountsClient.GetPaymentMethodsAsync(new GetPaymentMethodsRequest());
                TraditionalPaymentMethods = [.. paymentMethodsResponse.PaymentMethods];

                var filteredPaymentMethodIds = paymentMethodsResponse.PaymentMethods
                    //.Where(x => !PaymentAccounts.Select(y => y.PaymentMethod.Id).Contains(x.Id) && x.Id != "BLOCK_CHAINS")
                    .Where(x => !PaymentAccounts.Select(y => y.PaymentMethod.Id).Contains(x.Id))
                    .Select(x => x.Id);

                TraditionalPaymentMethodStrings = PaymentMethodsHelper.PaymentMethodsDictionary
                    .Where(x => filteredPaymentMethodIds.Contains(x.Key)).ToDictionary();

                break;
            }
            catch
            {

            }

            await Task.Delay(5_000);
        }

        await base.OnInitializedAsync();
    }

    // Does not work to validate crypto addresses. Only max/mins
    public async Task ValidateField(PaymentAccountFormField.Types.FieldId fieldId, string value)
    {
        try
        {
            using var payChannelHelper = new GrpcChannelHelper();
            var paymentAccountsClient = new PaymentAccountsClient(payChannelHelper.Channel);

            var response = await paymentAccountsClient.ValidateFormFieldAsync(new ValidateFormFieldRequest
            { 
                FieldId = PaymentAccountFormField.Types.FieldId.Address,
                Form = PaymentAccountForm,
                Value = value
            });
        }
        catch (Exception e)
        {

        }
    }

    public void HandleAccountClick(PaymentAccount paymentAccount)
    {
        PaymentAccountForm = null;
        SelectedPaymentAccount = paymentAccount;
    }

    public void ValidateModel(object? sender, FieldChangedEventArgs e)
    {
        if (sender is null || _messageStore is null || _editContext is null)
            return;

        _messageStore.Clear();

        if (e.FieldIdentifier.Model is not PaymentAccountFormField field)
            return;

        if (field.Value.Length > field.MaxLength && field.MaxLength != 0)
        {
            _messageStore.Add(() => field.Label, "Value cannot be greater than " + field.MaxLength);
        }
        else if (field.Value.Length < field.MinLength && field.MaxLength != 0)
        {
            _messageStore.Add(() => field.Label, "Value cannot be less than " + field.MinLength);
        }

        if (field.Id == PaymentAccountFormField.Types.FieldId.Email)
        {
            using var payChannelHelper = new GrpcChannelHelper();
            var paymentAccountsClient = new PaymentAccountsClient(payChannelHelper.Channel);
        }

        _editContext.NotifyValidationStateChanged();
    }

    public async Task DeleteAccountAsync(string paymentAccountId)
    {
        try
        {
            using var payChannelHelper = new GrpcChannelHelper();
            var paymentAccountsClient = new PaymentAccountsClient(payChannelHelper.Channel);

            var response = await paymentAccountsClient.DeletePaymentAccountAsync(new DeletePaymentAccountRequest
            {
                PaymentAccountId = paymentAccountId
            });

            PaymentAccounts = [..PaymentAccounts.Where(x => x.Id != paymentAccountId)];
            SelectedPaymentAccount = null;
        }
        catch
        {
            // Set ErrorCard TODO
        }
    }

    public async Task CreateCryptoPaymentAccountAsync()
    {
        using var payChannelHelper = new GrpcChannelHelper();
        var paymentAccountsClient = new PaymentAccountsClient(payChannelHelper.Channel);

        var response = await paymentAccountsClient.CreateCryptoCurrencyPaymentAccountAsync(CreateCryptoCurrencyPaymentAccountRequest);
        PaymentAccounts = [.. PaymentAccounts, response.PaymentAccount];

        CreateCryptoCurrencyPaymentAccountRequest = null;
    }

    public async Task CreatePaymentAccountAsync()
    {
        if (PaymentAccountForm is null)
            return;

        IsFetching = true;

        try
        {
            using var payChannelHelper = new GrpcChannelHelper();
            var paymentAccountsClient = new PaymentAccountsClient(payChannelHelper.Channel);

            var request = new CreatePaymentAccountRequest
            {
                PaymentAccountForm = PaymentAccountForm
            };

            var currencies = request.PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == Protobuf.PaymentAccountFormField.Types.FieldId.TradeCurrencies);
            if (currencies is not null)
            {
                request.PaymentAccountForm.Fields.Remove(currencies);

                var newCurrecies = new PaymentAccountFormField
                {
                    Id = PaymentAccountFormField.Types.FieldId.TradeCurrencies,
                    Label = currencies.Label,
                    Value = string.Join(",", SupportedCurrencyCodes.Where(x => x.IsSelected).Select(x => x.Code))
                };

                request.PaymentAccountForm.Fields.Add(newCurrecies);
            }

            var response = await paymentAccountsClient.CreatePaymentAccountAsync(request);
            PaymentAccounts = [.. PaymentAccounts, response.PaymentAccount];
            SelectedPaymentAccount = null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            IsFetching = false;
        }
    }
}
