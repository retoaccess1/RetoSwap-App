using Grpc.Core;
using HavenoSharp.Services;

namespace Manta.Services;

public interface IHavenoDaemonService
{
    Task<bool> GetIsDaemonInstalledAsync();
    Task InstallHavenoDaemonAsync();
    Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host, Action<string>? progressCb = default);
    Task<bool> TryStartTorAsync();
    Task<bool> WaitHavenoDaemonInitializedAsync(CancellationToken cancellationToken = default);
    Task<bool> WaitWalletInitializedAsync(CancellationToken cancellationToken = default);
    Task<bool> IsHavenoDaemonRunningAsync(CancellationToken cancellationToken = default);
    Task StopHavenoDaemonAsync();
}

public abstract class HavenoDaemonServiceBase : IHavenoDaemonService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHavenoVersionService _versionService;
    private readonly IHavenoAccountService _accountService;
    private readonly IHavenoWalletService _walletService;

    public HavenoDaemonServiceBase(IServiceProvider serviceProvider, IHavenoWalletService walletService, IHavenoVersionService versionService, IHavenoAccountService accountService)
    {
        _serviceProvider = serviceProvider;
        _accountService = accountService;
        _walletService = walletService;
        _versionService = versionService;
    }

    public abstract Task<bool> GetIsDaemonInstalledAsync();
    public abstract Task InstallHavenoDaemonAsync();
    public abstract Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host, Action<string>? progressCb = default);
    public abstract Task<bool> TryStartTorAsync();
    public abstract Task StopHavenoDaemonAsync();

    public async Task<bool> IsHavenoDaemonRunningAsync(CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < 2; i++)
        {
            try
            {
                // Will tell us its running but not if its initialized
                await _versionService.GetVersionAsync(cancellationToken: cancellationToken);

                return true;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (RpcException)
            {

            }
            catch (Exception)
            {

            }

            try
            {
                if (i == 1)
                {
                    break;
                }

                await Task.Delay(2_000, cancellationToken: cancellationToken);
            }
            catch (TaskCanceledException)
            {
                throw;
            }
        }

        return false;
    }

    public async Task<bool> WaitHavenoDaemonInitializedAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var isAppInitialized = await _accountService.IsAppInitializedAsync(cancellationToken: cancellationToken);
                if (isAppInitialized)
                    return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (RpcException)
            {

            }
            catch (Exception)
            {

            }

            try
            {
                await Task.Delay(2_000, cancellationToken: cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    public async Task<bool> WaitWalletInitializedAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                await _walletService.GetBalancesAsync(cancellationToken);
                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch
            {

            }

            try
            {
                await Task.Delay(2_000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }
    }
}
