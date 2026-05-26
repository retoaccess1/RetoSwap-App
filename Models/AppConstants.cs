using System.Reflection;

namespace Manta.Models;

public static class AppConstants
{
    public static readonly string DaemonUrl;
    public static readonly string Network;
    public static readonly string HavenoAppName;
    public static readonly string MatrixLink;
    public static readonly string GitHubLink;

    public const string ApplicationId = "com.retoswap";
    public const double MakerFeePctTraditional = 0.001;
    public const double TakerFeePctTraditional = 0.02;
    public const double MakerFeePctCrypto = 0.001;
    public const double TakerFeePctCrypto = 0.08;
    public const double PenaltyFeePct = 0.25;
    public const decimal MinimumTradeAmount = 0.05m;

    static AppConstants()
    {
        HavenoAppName = Assembly.GetExecutingAssembly()
           .GetCustomAttributes<AssemblyMetadataAttribute>()
           .FirstOrDefault(a => a.Key == "HavenoAppName")?.Value ?? string.Empty;

        Network = Assembly.GetExecutingAssembly()
           .GetCustomAttributes<AssemblyMetadataAttribute>()
           .FirstOrDefault(a => a.Key == "Network")?.Value ?? string.Empty;

        DaemonUrl = Assembly.GetExecutingAssembly()
           .GetCustomAttributes<AssemblyMetadataAttribute>()
           .FirstOrDefault(a => a.Key == "DaemonUrl")?.Value ?? string.Empty;

        SimplexLink = Assembly.GetExecutingAssembly()
           .GetCustomAttributes<AssemblyMetadataAttribute>()
           .FirstOrDefault(a => a.Key == "SimplexLink")?.Value ?? string.Empty;

        GitHubLink = Assembly.GetExecutingAssembly()
           .GetCustomAttributes<AssemblyMetadataAttribute>()
           .FirstOrDefault(a => a.Key == "GitHubLink")?.Value ?? string.Empty;

        MatrixLink = Assembly.GetExecutingAssembly()
           .GetCustomAttributes<AssemblyMetadataAttribute>()
           .FirstOrDefault(a => a.Key == "MatrixLink")?.Value ?? string.Empty;
    }
}
