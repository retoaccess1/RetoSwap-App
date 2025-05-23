using HavenoSharp.Services;
using Manta.Helpers;

namespace Manta.Singletons;

public class DaemonInfoSingleton
{
    private readonly IServiceProvider _serviceProvider;

    //public List<UrlConnection> UrlConnections { get; private set; } = [];
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
                OnDaemonInfoFetch?.Invoke(true);

                //var connectionsResponse = await xmrConnectionsClient.GetConnectionsAsync(new GetConnectionsRequest());

                //UrlConnections = [.. connectionsResponse.Connections];

                using var scope = _serviceProvider.CreateScope();
                var xmrNodeService = _serviceProvider.GetRequiredService<IHavenoXmrNodeService>();

                XMRNodeIsRunning = await xmrNodeService.IsXmrNodeOnlineAsync();
            }
            catch (Exception e)
            {
                //Console.WriteLine(e);
            }
            finally
            {
                OnDaemonInfoFetch?.Invoke(false);
            }

            await Task.Delay(5_000);
        }
    }
}
