using HavenoSharp.Models;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Manta.Extensions;

public static partial class PaymentAccountToFieldsExtensions
{
    static IEnumerable<string> SplitCamelCase(this string input)
    {
        return CamelCaseRegex().Split(input).Where(str => !string.IsNullOrEmpty(str));
    }

    private static IEnumerable<KeyValuePair<string, string>> GetFields(PropertyInfo? propertyInfo, object orignalObj)
    {
        var val = propertyInfo?.GetValue(orignalObj);

        var properties = val?.GetType().GetProperties()
            .Where(x => x.Name != "Parser" && x.Name != "Descriptor");

        if (properties is null)
            yield break;

        foreach (var property in properties)
        {
            var value = property.GetValue(val);
            if (value is null)
                continue;

            if (property.Name == "AcceptedCountryCodes")
                continue;

            switch (value)
            {
                case string str:
                    yield return new KeyValuePair<string, string>(string.Join(' ', property.Name.SplitCamelCase()), str);
                    break;
                case IEnumerable<string> strList:
                    yield return new KeyValuePair<string, string>(string.Join(' ', property.Name.SplitCamelCase()), string.Join(", ", strList));
                    break;
                case object obj:
                    {
                        var innerProperties = obj.GetType().GetProperties();
                        foreach (var innerProperty in innerProperties)
                        {
                            var innerValue = innerProperty.GetValue(obj);
                            if (innerValue is null)
                                continue;

                            yield return new KeyValuePair<string, string>(string.Join(' ', innerProperty.Name.SplitCamelCase()), innerValue.ToString() ?? string.Empty);
                        }
                    }
                    break;
                default: continue;
            }
        }
    }

    /// <summary>
    /// Convert a PaymentAccountPayload to a list of field name and field values
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<KeyValuePair<string, string>> Convert(this PaymentAccountPayload paymentAccountPayload)
    {
        var paymentAccountPropertyInfo = typeof(PaymentAccountPayload).GetProperties()
            .Where(x => x.GetValue(paymentAccountPayload) is not null && x.Name != "Parser" && x.Name != "Id" && x.Name != "MaxTradePeriod" && x.Name != "ExcludeFromJsonData" && x.Name != "PaymentMethodId" && x.Name != "MessageCase" && x.Name != "Descriptor")
            .FirstOrDefault();

        return GetFields(paymentAccountPropertyInfo, paymentAccountPayload);
    }

    [GeneratedRegex(@"([A-Z]?[a-z]+)")]
    private static partial Regex CamelCaseRegex();
}
