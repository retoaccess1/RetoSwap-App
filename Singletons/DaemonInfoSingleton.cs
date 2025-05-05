using Manta.Helpers;
using Haveno.Proto.Grpc;

using static Haveno.Proto.Grpc.XmrConnections;
using static Haveno.Proto.Grpc.XmrNode;

namespace Manta.Singletons;

public class DaemonInfoSingleton
{
    public List<UrlConnection> UrlConnections { get; private set; } = [];
    public bool XMRNodeIsRunning { get; private set; }

    public event Action<bool>? OnDaemonInfoFetch;

    public DaemonInfoSingleton()
    {
        Task.Run(PollDaemon);
    }

    private async Task PollDaemon()
    {
        while (true)
        {
            try
            {
                OnDaemonInfoFetch?.Invoke(true);

                using var grpcChannelHelper = new GrpcChannelHelper();
                var xmrConnectionsClient = new XmrConnectionsClient(grpcChannelHelper.Channel);
                var connectionsResponse = await xmrConnectionsClient.GetConnectionsAsync(new GetConnectionsRequest());

                UrlConnections = [.. connectionsResponse.Connections];

                var xmrNodeClient = new XmrNodeClient(grpcChannelHelper.Channel);
                var isXmrNodeOnlineResponse = await xmrNodeClient.IsXmrNodeOnlineAsync(new IsXmrNodeOnlineRequest());

                XMRNodeIsRunning = isXmrNodeOnlineResponse.IsRunning;

                var settingsResponse = await xmrNodeClient.GetXmrNodeSettingsAsync(new GetXmrNodeSettingsRequest());
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
