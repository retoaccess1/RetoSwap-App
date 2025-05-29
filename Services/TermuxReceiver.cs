#if ANDROID

using Android.App;
using Android.Content;

namespace Manta.Services;

[BroadcastReceiver(
    Name = "com.companyname.manta.TermuxReceiver",
    Enabled = true,
    Exported = true)]
[IntentFilter(["com.companyname.manta.TERMUX_READY"])]
public class TermuxReceiver : BroadcastReceiver
{
    public static TaskCompletionSource TaskCompletionSource { get; private set; } = new();

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (!TaskCompletionSource.Task.IsCompleted)
            TaskCompletionSource.SetResult();
    }
}

#endif