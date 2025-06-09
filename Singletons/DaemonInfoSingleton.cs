using HavenoSharp.Services;

namespace Manta.Singletons;

public class DaemonInfoSingleton : SingletonBase
{
    private readonly IServiceProvider _serviceProvider;

    public bool XMRNodeIsRunning { get; private set; }

    public event Action<bool>? OnDaemonInfoFetch;

    public DaemonInfoSingleton(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Task.Run(PollDaemon);
    }

    private async Task PollDaemon()
    {
        while (true)
        {
            try
            {
                await _pauseSource.Token.WaitIfPausedAsync();

                OnDaemonInfoFetch?.Invoke(true);

                using var scope = _serviceProvider.CreateScope();
                var xmrNodeService = _serviceProvider.GetRequiredService<IHavenoXmrNodeService>();

                XMRNodeIsRunning = await xmrNodeService.IsXmrNodeOnlineAsync();
            }
            catch (Exception)
            {

            }
            finally
            {
                OnDaemonInfoFetch?.Invoke(false);
            }

            await Task.Delay(5_000);
        }
    }
}
