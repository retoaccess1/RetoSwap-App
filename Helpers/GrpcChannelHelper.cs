using Grpc.Net.Client;

namespace Manta.Helpers;

public interface IGrpcChannelHelper
{
    GrpcChannel Channel { get; }
}

public sealed class GrpcChannelHelper : IGrpcChannelHelper, IDisposable
{
    private const string _password = "apitest";
    private const string _host = "http://127.0.0.1:3201";

    public GrpcChannel Channel { get; }

    public GrpcChannelHelper(bool noTimeout = false)
    {
        // Does this get disposed?
        // Might need to inject httpclient
        var httpClient = new HttpClient(new SocketsHttpHandler())
        {
            DefaultRequestVersion = new Version(2, 0),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        // Mainly for streaming
        if (noTimeout)
            httpClient.Timeout = Timeout.InfiniteTimeSpan;

        // TODO configable
        httpClient.DefaultRequestHeaders.Add("password", _password);

        Channel = GrpcChannel.ForAddress(_host, new GrpcChannelOptions { HttpClient = httpClient });
    }

    public void Dispose()
    {
        Channel.Dispose();
    }
}
