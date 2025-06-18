namespace Manta.Models;

public static class PauseTokenSource
{
    private static volatile TaskCompletionSource<bool> _resumeTcs = new();

    public static void Pause()
    {
        Interlocked.Exchange(ref _resumeTcs, new()).TrySetCanceled();
    }

    public static void Resume()
    {
        _resumeTcs.TrySetResult(true);
    }

    public static Task WaitWhilePausedAsync()
    {
        return _resumeTcs.Task;
    }
}
