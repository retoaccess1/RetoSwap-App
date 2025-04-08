using Manta.Helpers;
using static Haveno.Proto.Grpc.Account;

namespace Manta.Services;

public interface IAccountService
{

}

public sealed class AccountService : IDisposable, IAccountService
{
    private readonly AccountClient _accountClient;
    private readonly GrpcChannelHelper _grpcChannelHelper;

    public AccountService()
    {
        _grpcChannelHelper = new();
        _accountClient = new AccountClient(_grpcChannelHelper.Channel);
    }

    public void Dispose()
    {
        _grpcChannelHelper.Dispose();
    }
}
