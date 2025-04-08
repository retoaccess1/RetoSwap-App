using Grpc.Net.Client;

namespace Manta.Services;

public interface IDaemonGrpcChannel 
{

}

public sealed class DaemonGrpcChannel : IDaemonGrpcChannel, IDisposable
{
    public void Dispose()
    {
        
    }
}
