using System.Reflection;

namespace Manta.Models;

public static class AppConstants
{
    public static readonly string DaemonUrl;
    public static readonly string Network;
    public static readonly string HavenoAppName;

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
    }
}
