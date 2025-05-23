using Android.App;
using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace Manta.Platforms.Android.Services;

public static class TermuxPermissionHelper
{
    private static TaskCompletionSource<bool> _tcs;
    private static readonly Activity _activity = Platform.CurrentActivity;

    public static async Task<bool> RequestRunCommandPermissionAsync()
    {
        if (ContextCompat.CheckSelfPermission(_activity, "com.termux.permission.RUN_COMMAND") == (int)Permission.Granted)
            return true;

        _tcs = new TaskCompletionSource<bool>();

        ActivityCompat.RequestPermissions(_activity, ["com.termux.permission.RUN_COMMAND"], 0);

        // Increase
        return await _tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public static void HandlePermissionResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (requestCode == 0 && _tcs != null)
        {
            bool granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            _tcs.TrySetResult(granted);
        }
    }
}
