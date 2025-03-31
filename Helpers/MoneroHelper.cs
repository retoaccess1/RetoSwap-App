namespace Manta.Helpers;

public static class MoneroHelper
{
    public static decimal ToMonero(this ulong amount)
    {
        return amount / 1_000_000_000_000m;
    }

    public static ulong ToPiconero(this decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, decimal.Zero);

        amount = Math.Round(amount, 12);
        amount *= 1_000_000_000_000m;

        return checked((ulong)amount);
    }
}
