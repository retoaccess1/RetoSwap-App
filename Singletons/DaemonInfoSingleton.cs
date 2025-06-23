using HavenoSharp.Services;
using Manta.Models;

namespace Manta.Singletons;

public class DaemonInfoSingleton 
{
    private readonly IServiceProvider _serviceProvider;

    public bool XMRNodeIsRunning { get; private set; }
    public string ConnectedMoneroNodeUrl { get; private set; } = string.Empty;

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
                await PauseTokenSource.WaitWhilePausedAsync();

                OnDaemonInfoFetch?.Invoke(true);

                using var scope = _serviceProvider.CreateScope();
                var xmrNodeService = _serviceProvider.GetRequiredService<IHavenoXmrNodeService>();

                XMRNodeIsRunning = await xmrNodeService.IsXmrNodeOnlineAsync();

                var response = await xmrNodeService.GetMoneroNodeAsync();
                ConnectedMoneroNodeUrl = response.Url;
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
