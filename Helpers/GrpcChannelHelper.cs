using Grpc.Net.Client;
using Grpc.Net.Client.Web;

namespace Manta.Helpers;

public interface IGrpcChannelHelper
{
    GrpcChannel Channel { get; }
}

public sealed class GrpcChannelHelper : IGrpcChannelHelper, IDisposable
{
    // Not sure these should be static but works for now
    public static string Password { get; set; } = string.Empty;
    public static string Host { get; set; } = string.Empty;

    public GrpcChannel Channel { get; }

    public GrpcChannelHelper(bool noTimeout = false)
    {
        HttpClient httpClient;

        if (Host != "http://127.0.0.1:3201")
        {
#if ANDROID
            httpClient = new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new AndroidSocks5Handler()));
#else
            httpClient = new HttpClient();
#endif
        }
        else
        {
            // Does this get disposed?
            // Might need to inject httpclient
            httpClient = new HttpClient(new SocketsHttpHandler())
            {
                DefaultRequestVersion = new Version(2, 0),
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
            };
        }

        // Mainly for streaming
        if (noTimeout)
            httpClient.Timeout = Timeout.InfiniteTimeSpan;

        // TODO configable
        httpClient.DefaultRequestHeaders.Add("password", Password);

        Channel = GrpcChannel.ForAddress(Host, new GrpcChannelOptions { HttpClient = httpClient });
    }

    public void Dispose()
    {
        Channel.Dispose();
    }
}
