namespace Manta.Models;

public class TradeStatistic
{
    public string Arbitrator { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string MakerDepositTxId { get; set; } = string.Empty;
    public string TakerDepositTxId { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public long Amount { get; set; }
    public DateTime Date { get; set; }
    public long Price { get; set; }
    public Dictionary<string, string> ExtraData { get; set; } = [];
    public byte[] Hash { get; set; } = [];
}
