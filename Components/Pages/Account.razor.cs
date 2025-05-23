using Grpc.Core;
using HavenoSharp.Extensions;
using HavenoSharp.Models;
using HavenoSharp.Models.Requests;
using HavenoSharp.Services;
using Manta.Components.Reusable;
using Manta.Helpers;
using Manta.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Manta.Components.Pages;

public partial class Account : ComponentBase
{
    [Inject]
    public IHavenoPaymentAccountService PaymentAccountService { get; set; } = default!;

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
    public bool ShowDeleteAccountModal { get; set; }

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

            // TODO cache this and clear when daemon version changes
            var paymentAccountForm = Task.Run(() => PaymentAccountService.GetPaymentAccountFormAsync(SelectedPaymentMethodId)).GetAwaiter().GetResult();

            // Add a default account name
            var accountNameField = paymentAccountForm.Fields.FirstOrDefault(x => x.Id == FieldId.ACCOUNT_NAME);
            if (accountNameField is not null)
            {
                accountNameField.Value = SelectedPaymentMethodId;
            }

            PaymentAccountForm = paymentAccountForm;

            var acceptedCountriesField = PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == FieldId.ACCEPTED_COUNTRY_CODES);
            if (acceptedCountriesField is not null)
            {
                AcceptedEUSEPACountries = acceptedCountriesField.SupportedSepaEuroCountries
                    .Select(x => new SelectedAcceptedCountry { Code = x.Code, CodeWithCountry = $"{x.Name} ({x.Code})", IsSelected = true })
                    .ToList();

                AcceptedNonEUSEPACountries = acceptedCountriesField.SupportedSepaNonEuroCountries
                    .Select(x => new SelectedAcceptedCountry { Code = x.Code, CodeWithCountry = $"{x.Name} ({x.Code})", IsSelected = true })
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
                PaymentAccounts = await PaymentAccountService.GetPaymentAccountsAsync();
                TraditionalPaymentMethods = await PaymentAccountService.GetPaymentMethodsAsync();

                var filteredPaymentMethodIds = TraditionalPaymentMethods
                    .Select(x => x.Id);

                TraditionalPaymentMethodStrings = PaymentMethodsHelper.PaymentMethodsDictionary
                    .Where(x => filteredPaymentMethodIds.Contains(x.Key)).OrderBy(x => x.Value).ToDictionary();

                if (!string.IsNullOrEmpty(AccountToCreate))
                {
                    SelectedPaymentMethodId = AccountToCreate;
                }

                break;
            }
            catch (Exception)
            {

            }

            await Task.Delay(2_000);
        }

        await base.OnInitializedAsync();
    }

    public void HandleCountryChanged(string country)
    {
        if (PaymentAccountForm is null)
            return;

        var countryField = PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == FieldId.COUNTRY);
        if (countryField is not null)
        {
            countryField.Value = country;
        }
    }

    // Does not work to validate crypto addresses. Only max/mins
    public async Task ValidateField(FieldId fieldId, string value)
    {
        try
        {
            var response = await PaymentAccountService.ValidateFormFieldAsync(new ValidateFormFieldRequest
            { 
                FieldId = FieldId.ADDRESS,
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

        try
        {
            var response = Task.Run(() => PaymentAccountService.ValidateFormFieldAsync(new ValidateFormFieldRequest
            {
                FieldId = field.Id,
                Form = PaymentAccountForm,
                Value = field.Value
            })).GetAwaiter().GetResult();
        }
        catch (RpcException ex)
        {
            _messageStore.Add(() => field.Label, ex.GetErrorMessage());
        }

        _editContext.NotifyValidationStateChanged();
    }

    public async Task DeleteAccountAsync(string paymentAccountId)
    {
        await PaymentAccountService.DeletePaymentAccountAsync(paymentAccountId);

        PaymentAccounts = [.. PaymentAccounts.Where(x => x.Id != paymentAccountId)];
        SelectedPaymentAccount = null;
        ShowDeleteAccountModal = false;
    }

    public async Task CreateCryptoPaymentAccountAsync()
    {
        var createdCryptoAccount = await PaymentAccountService.CreateCryptoCurrencyPaymentAccountAsync(CreateCryptoCurrencyPaymentAccountRequest);
        PaymentAccounts = [.. PaymentAccounts, createdCryptoAccount];

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
            var request = new CreatePaymentAccountRequest
            {
                PaymentAccountForm = PaymentAccountForm
            };

            var currenciesField = request.PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == FieldId.TRADE_CURRENCIES);
            if (currenciesField is not null)
            {
                currenciesField.Value = string.Join(",", SupportedCurrencyCodes.Where(x => x.IsSelected).Select(x => x.Code));
            }

            var acceptedCountriesField = request.PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == FieldId.ACCEPTED_COUNTRY_CODES);
            if (acceptedCountriesField is not null)
            {
                acceptedCountriesField.Value = string.Join(",", [..AcceptedEUSEPACountries.Where(x => x.IsSelected).Select(x => x.Code), ..AcceptedNonEUSEPACountries.Where(x => x.IsSelected).Select(x => x.Code)]);
            }

            if (SelectedPaymentMethodId == "F2F" || SelectedPaymentMethodId == "MONEY_GRAM") 
            {
                var countriesField = request.PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == FieldId.COUNTRY);
                if (countriesField is not null)
                {

                }            
            }

            var createdPaymentAccount = await PaymentAccountService.CreatePaymentAccountAsync(request);
            PaymentAccounts = [.. PaymentAccounts, createdPaymentAccount];

            var filteredPaymentMethodIds = TraditionalPaymentMethods
                .Select(x => x.Id);

            TraditionalPaymentMethodStrings = PaymentMethodsHelper.PaymentMethodsDictionary
                .Where(x => filteredPaymentMethodIds.Contains(x.Key)).OrderBy(x => x.Value).ToDictionary();

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
