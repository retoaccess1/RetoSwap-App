namespace Manta.Singletons;

public class PauseTokenSource
{
    private volatile TaskCompletionSource<bool> _resumeTcs = new();

    public PauseToken Token => new(this);

    public void Pause()
    {
        Interlocked.Exchange(ref _resumeTcs, new()).TrySetCanceled();
    }

    public void Resume()
    {
        _resumeTcs.TrySetResult(true);
    }

    internal Task WaitWhilePausedAsync()
    {
        return _resumeTcs.Task;
    }
}

public readonly struct PauseToken
{
    private readonly PauseTokenSource _source;

    public PauseToken(PauseTokenSource source)
    {
        _source = source;
    }

    public Task WaitIfPausedAsync()
    {
        return _source.WaitWhilePausedAsync();
    }
}

public abstract class SingletonBase
{
    protected PauseTokenSource _pauseSource = new();

    public void Pause()
    {
        _pauseSource.Pause();
    }

    public void Resume()
    {
        _pauseSource.Resume();
    }
}
