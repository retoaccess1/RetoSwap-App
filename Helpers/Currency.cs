using System.Globalization;

namespace Manta.Helpers;

public enum Currency
{
    AUD,
    BRL,
    CAD,
    CHF,
    CNY,
    EUR,
    GBP,
    HKD,
    INR,
    JPY,
    KRW,
    MXN,
    NOK,
    NZD,
    SEK,
    SGD,
    TRY,
    USD,
    RUB,
    ZAR
}

public enum CryptoCurrency
{
    BTC,
    BCH,
    ETH,
    LTC,
    DAI_ERC20,
    USDT_ERC20,
    USDT_TRC20,
    USDC_ERC20
}

public static class CryptoCurrencyHelper
{
    public static Dictionary<string, string> CryptoCurrenciesDictionary = new()
    {
        { "BTC", "Bitcoin" },
        { "BCH", "Bitcoin Cash" },
        { "ETH", "Ether" },
        { "LTC", "Litecoin" },
        { "DAI-ERC20", "Dai Stablecoin (ERC20)" },
        { "USDT-ERC20", "Tether USD (ERC20)" },
        { "USDT-TRC20", "Tether USD (TRC20)" },
        { "USDC-ERC20", "USD Coin (ERC20)" }
    };
}

public static class CurrencyCultureInfo
{
    public static CultureInfo GetCultureInfoForCurrency(Currency currency)
    {
        CultureInfo cultureInfo;

        switch (currency)
        {
            case Currency.AUD:
                cultureInfo = new("en-AU");
                break;
            case Currency.BRL:
                cultureInfo = new("pt-BR");
                break;
            case Currency.CAD:
                cultureInfo = new("en-CA");
                break;
            case Currency.CHF:
                cultureInfo = new("fr-CH");
                break;
            case Currency.CNY:
                cultureInfo = new("zh-CN");
                break;
            case Currency.EUR:
                cultureInfo = new("fr-FR");
                break;
            case Currency.GBP:
                cultureInfo = new("en-GB");
                break;
            case Currency.HKD:
                cultureInfo = new("zh-HK");
                break;
            case Currency.INR:
                cultureInfo = new("hi-IN");
                break;
            case Currency.JPY:
                cultureInfo = new("ja-JP");
                break;
            case Currency.KRW:
                cultureInfo = new("ko-KR");
                break;
            case Currency.MXN:
                cultureInfo = new("es-MX");
                break;
            case Currency.NOK:
                cultureInfo = new("nb-NO");
                break;
            case Currency.NZD:
                cultureInfo = new("en-NZ");
                break;
            case Currency.SEK:
                cultureInfo = new("sv-SE");
                break;
            case Currency.SGD:
                cultureInfo = new("zh-SG");
                break;
            case Currency.TRY:
                cultureInfo = new("tr-TR");
                break;
            case Currency.USD:
                cultureInfo = new("en-US");
                break;
            case Currency.RUB:
                cultureInfo = new("ru-RU");
                break;
            case Currency.ZAR:
                cultureInfo = new("en-ZA");
                break;
            default: throw new ArgumentException("currency is not valid");
        }

        return cultureInfo;
    }

    public static NumberFormatInfo GetFormatForCurrency(Currency currency)
    {
        return GetCultureInfoForCurrency(currency).NumberFormat;
    }

    public static string GetCurrencyFullName(Currency currency)
    {
        RegionInfo region = new(GetCultureInfoForCurrency(currency).Name);
        return region.CurrencyEnglishName + $" ({currency})";
    }

    public static IEnumerable<string> GetCurrencyFullNames()
    {
        foreach (var currency in Enum.GetValues(typeof(Currency)))
        {
            RegionInfo region = new(GetCultureInfoForCurrency((Currency)currency).Name);
            yield return region.CurrencyEnglishName + $" ({currency})";
        }
    }

    public static IEnumerable<KeyValuePair<string, string>> GetCurrencyFullNamesAndCurrencyCodeDictionary()
    {
        foreach (var currency in Enum.GetValues(typeof(Currency)))
        {
            RegionInfo region = new(GetCultureInfoForCurrency((Currency)currency).Name);
            yield return new KeyValuePair<string, string>(currency.ToString()!, region.CurrencyEnglishName + $" ({currency})");
        }
    }
}
