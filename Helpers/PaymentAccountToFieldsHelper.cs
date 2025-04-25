using Protobuf;

namespace Manta.Helpers;

public static class PaymentAccountToFieldsHelper
{

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
            yield return new KeyValuePair<string, string>(property.Name, property.GetValue(val)?.ToString() ?? "");
        }
    }
}
