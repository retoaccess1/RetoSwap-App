using Haveno.Proto.Grpc;
using Manta.Helpers;
using static Haveno.Proto.Grpc.GetVersion;
using static Haveno.Proto.Grpc.Wallets;

namespace Manta.Singletons;

public class DaemonConnectionSingleton
{
    public string Version { get; private set; } = string.Empty;
    public bool IsConnected { get; private set; }
    public Action<bool>? OnConnectionChanged;

    public bool IsWalletAvailable { get; private set; }
    public Action<bool>? OnWalletAvailabilityChanged;

    public DaemonConnectionSingleton()
    {
        Task.Run(PollDaemon);
        Task.Run(PollWallet);
    }

    private async Task PollWallet()
    {
        while (true)
        {
            try
            {

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {

            }

            await Task.Delay(5_000);
        }
    }

    private async Task PollDaemon()
    {
        while (true)
        {
            try
            {
                // Checks if the daemon is running, it could still be that its not fully initialized so things like wallet won't work.
                using var grpcChannelHelper = new GrpcChannelHelper();
                var client = new GetVersionClient(grpcChannelHelper.Channel);

                var response = await client.GetVersionAsync(new GetVersionRequest());
                Version = response.Version;

                // If connection status has changed
                if (!IsConnected)
                {
                    IsConnected = true;
                    OnConnectionChanged?.Invoke(IsConnected);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                if (IsConnected)
                {
                    IsConnected = false;
                    OnConnectionChanged?.Invoke(IsConnected);
                }
            }
            finally
            {

            }

            await Task.Delay(5_000);
        }
    }
}
