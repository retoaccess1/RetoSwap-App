using Protobuf;
using System.Text.RegularExpressions;

namespace Manta.Helpers;

public static partial class PaymentAccountToFieldsHelper
{
    static IEnumerable<string> SplitCamelCase(this string input)
    {
        return CamelCaseRegex().Split(input).Where(str => !string.IsNullOrEmpty(str));
    }

    /// <summary>
    /// Convert a PaymentAccountPayload to a list of field name and field values
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<KeyValuePair<string, string>> Convert(this PaymentAccountPayload paymentAccountPayload)
    {
        var paymentAccount = typeof(PaymentAccountPayload).GetProperties()
            .Where(x => x.GetValue(paymentAccountPayload) is not null && x.Name != "Parser" && x.Name != "Id" && x.Name != "MaxTradePeriod" && x.Name != "ExcludeFromJsonData" && x.Name != "PaymentMethodId" && x.Name != "MessageCase" && x.Name != "Descriptor")
            .FirstOrDefault();

        var val = paymentAccount?.GetValue(paymentAccountPayload);

        var properties = val?.GetType().GetProperties()
            .Where(x => x.Name != "Parser" && x.Name != "Descriptor");

        foreach(var property in properties)
        {
            yield return new KeyValuePair<string, string>(string.Join(' ', property.Name.SplitCamelCase()), property.GetValue(val)?.ToString() ?? "");
        }
    }

    [GeneratedRegex(@"([A-Z]?[a-z]+)")]
    private static partial Regex CamelCaseRegex();
}
