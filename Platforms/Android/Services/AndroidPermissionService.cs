using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;

namespace Manta.Services;

public enum PermissionCodes : int
{
    IgnoreBatteryOptimizations = 7230441
}

public static class AndroidPermissionService
{
    private static TaskCompletionSource<bool>? _ignoreBatteryOptimizationsTCS;

    public static void SetPermissionResult(PermissionCodes permissionCode, Result resultCode)
    {
        try
        {
            switch (permissionCode)
            {
                case PermissionCodes.IgnoreBatteryOptimizations:
                    if (_ignoreBatteryOptimizationsTCS is not null && !_ignoreBatteryOptimizationsTCS.Task.IsCompleted)
                    {
                        // resultCode is always cancelled for some reason so we have to manually check
                        _ignoreBatteryOptimizationsTCS.SetResult(GetIgnoreBatteryOptimizationsEnabled());
                    }
                    break;
                default: break;
            }
        }
        catch
        {

        }
    }

    public static bool GetIgnoreBatteryOptimizationsEnabled()
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
            return false;

        var packageName = activity.PackageName;
        var pm = (PowerManager?)activity.ApplicationContext?.GetSystemService(Context.PowerService);
        if (pm is null)
            return false;

        return pm.IsIgnoringBatteryOptimizations(packageName);
    }

    public static async Task<bool> RequestIgnoreBatteryOptimizationsAsync()
    {
        try
        {
            if (GetIgnoreBatteryOptimizationsEnabled())
                return true;

            var activity = Platform.CurrentActivity;
            if (activity is null)
                return false;

            var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations);
            intent.SetData(Android.Net.Uri.Parse($"package:{activity.PackageName}"));

            _ignoreBatteryOptimizationsTCS = new();

            activity.StartActivityForResult(intent, (int)PermissionCodes.IgnoreBatteryOptimizations);

            return await _ignoreBatteryOptimizationsTCS.Task;
        }
        catch
        {
            return false;
        }
    }
}
