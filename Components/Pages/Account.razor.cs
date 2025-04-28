using Grpc.Core;
using Haveno.Proto.Grpc;
using Manta.Components.Reusable;
using Manta.Extensions;
using Manta.Helpers;
using Manta.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Protobuf;

using static Haveno.Proto.Grpc.PaymentAccounts;

namespace Manta.Components.Pages;


// Bug where delete account modal keeps popping up
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
    public List<SelectedAcceptedCountry> AcceptedNonEUSEPACountries { get; set; } = [];
    public List<SelectedAcceptedCountry> AcceptedEUSEPACountries { get; set; } = [];
    public List<PaymentAccount> PaymentAccounts { get; set; } = [];
    public List<PaymentAccount> CryptoPaymentAccounts { get; set; } = [];
    public List<PaymentMethod> TraditionalPaymentMethods { get; set; } = [];
    public List<PaymentMethod> CryptoPaymentMethods { get; set; } = [];

    public long MaxTradePeriod { get; set; }
    public long MaxTradeLimit { get; set; }

    public SearchableDropdown PaymentMethodSearchableDropdown { get; set; } = default!;

    public bool CustomAccountNameEnabled { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string? AccountToCreate { get; set; }

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
                AcceptedNonEUSEPACountries = [];
                AcceptedEUSEPACountries = [];
                PaymentAccountForm = default!;
                return;
            }

            if (field == "BLOCK_CHAINS")
            {
                CreateCryptoCurrencyPaymentAccountRequest = new();
            }
            else
            {
                CreateCryptoCurrencyPaymentAccountRequest = null;
            }

            var paymentMethod = TraditionalPaymentMethods
                .FirstOrDefault(x => x.Id == SelectedPaymentMethodId);

            if (paymentMethod is null)
                throw new Exception("paymentMethod was null");

            MaxTradePeriod = paymentMethod.MaxTradePeriod;
            MaxTradeLimit = paymentMethod.MaxTradeLimit;

            SupportedCurrencyCodes = paymentMethod
                .SupportedAssetCodes.Select(x => new SelectedCurrency { Code = x, IsSelected = false })
                .ToList();

            using var payChannelHelper = new GrpcChannelHelper();
            var paymentAccountsClient = new PaymentAccountsClient(payChannelHelper.Channel);

            // This is a network call in a setter, should not do this 
            var getPaymentAccountFormResponse = paymentAccountsClient.GetPaymentAccountForm(new GetPaymentAccountFormRequest
            {
                PaymentMethodId = SelectedPaymentMethodId
            });

            // Add a default account name
            var accountNameField = getPaymentAccountFormResponse.PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == PaymentAccountFormField.Types.FieldId.AccountName);
            if (accountNameField is not null)
            {
                accountNameField.Value = SelectedPaymentMethodId;
            }

            PaymentAccountForm = getPaymentAccountFormResponse.PaymentAccountForm;

            var acceptedCountriesField = PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == PaymentAccountFormField.Types.FieldId.AcceptedCountryCodes);
            if (acceptedCountriesField is not null)
            {
                AcceptedEUSEPACountries = acceptedCountriesField.SupportedSepaEuroCountries
                    .Select(x => new SelectedAcceptedCountry { Code = x.Code, IsSelected = true })
                    .ToList();

                AcceptedNonEUSEPACountries = acceptedCountriesField.SupportedSepaNonEuroCountries
                    .Select(x => new SelectedAcceptedCountry { Code = x.Code, IsSelected = true })
                    .ToList();
            }

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
                    .Select(x => x.Id);

                TraditionalPaymentMethodStrings = PaymentMethodsHelper.PaymentMethodsDictionary
                    .Where(x => filteredPaymentMethodIds.Contains(x.Key)).ToDictionary();

                if (!string.IsNullOrEmpty(AccountToCreate))
                {
                    SelectedPaymentMethodId = AccountToCreate;
                }

                break;
            }
            catch
            {

            }

            await Task.Delay(5_000);
        }

        await base.OnInitializedAsync();
    }

    public void HandleCountryChanged(string country)
    {
        if (PaymentAccountForm is null)
            return;

        var countryField = PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == PaymentAccountFormField.Types.FieldId.Country);
        if (countryField is not null)
        {
            countryField.Value = country;
        }
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
        PaymentMethodSearchableDropdown.Clear();
    }

    public void ValidateModel(object? sender, FieldChangedEventArgs e)
    {
        if (sender is null || _messageStore is null || _editContext is null)
            return;

        _messageStore.Clear();

        if (e.FieldIdentifier.Model is not PaymentAccountFormField field)
            return;

        using var payChannelHelper = new GrpcChannelHelper();
        var paymentAccountsClient = new PaymentAccountsClient(payChannelHelper.Channel);

        try
        {
            // This blocks and is a network call, not great to do synchronously
            var response = paymentAccountsClient.ValidateFormField(new ValidateFormFieldRequest
            {
                FieldId = field.Id,
                Form = PaymentAccountForm,
                Value = field.Value
            });
        }
        catch (RpcException ex)
        {
            _messageStore.Add(() => field.Label, ex.GetErrorMessage());
        }

        _editContext.NotifyValidationStateChanged();
    }

    public async Task DeleteAccountAsync(string paymentAccountId)
    {
        using var payChannelHelper = new GrpcChannelHelper();
        var paymentAccountsClient = new PaymentAccountsClient(payChannelHelper.Channel);

        var response = await paymentAccountsClient.DeletePaymentAccountAsync(new DeletePaymentAccountRequest
        {
            PaymentAccountId = paymentAccountId
        });

        PaymentAccounts = [.. PaymentAccounts.Where(x => x.Id != paymentAccountId)];
        SelectedPaymentAccount = null;
        ShowDeleteAccountModal = false;
    }

    public async Task CreateCryptoPaymentAccountAsync()
    {
        using var payChannelHelper = new GrpcChannelHelper();
        var paymentAccountsClient = new PaymentAccountsClient(payChannelHelper.Channel);

        var response = await paymentAccountsClient.CreateCryptoCurrencyPaymentAccountAsync(CreateCryptoCurrencyPaymentAccountRequest);
        PaymentAccounts = [.. PaymentAccounts, response.PaymentAccount];

        CreateCryptoCurrencyPaymentAccountRequest = null;
        PaymentAccountForm = null;
        PaymentMethodSearchableDropdown.Clear();
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

            var currenciesField = request.PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == PaymentAccountFormField.Types.FieldId.TradeCurrencies);
            if (currenciesField is not null)
            {
                currenciesField.Value = string.Join(",", SupportedCurrencyCodes.Where(x => x.IsSelected).Select(x => x.Code));
            }

            var acceptedCountriesField = request.PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == PaymentAccountFormField.Types.FieldId.AcceptedCountryCodes);
            if (acceptedCountriesField is not null)
            {
                acceptedCountriesField.Value = string.Join(",", [..AcceptedEUSEPACountries.Where(x => x.IsSelected).Select(x => x.Code), ..AcceptedNonEUSEPACountries.Where(x => x.IsSelected).Select(x => x.Code)]);
            }

            var response = await paymentAccountsClient.CreatePaymentAccountAsync(request);
            PaymentAccounts = [.. PaymentAccounts, response.PaymentAccount];

            var filteredPaymentMethodIds = TraditionalPaymentMethods
                .Select(x => x.Id);

            TraditionalPaymentMethodStrings = PaymentMethodsHelper.PaymentMethodsDictionary
                .Where(x => filteredPaymentMethodIds.Contains(x.Key)).ToDictionary();

            SelectedPaymentAccount = null;
            PaymentAccountForm = null;
            CreateCryptoCurrencyPaymentAccountRequest = null;
            SelectedPaymentMethodId = string.Empty;
            PaymentMethodSearchableDropdown.Clear();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            IsFetching = false;
        }
    }
}
