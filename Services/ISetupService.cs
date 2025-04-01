namespace Manta.Services;

public interface ISetupService
{
    Task InitialSetupAsync();
    Task<DaemonStatus> GetDaemonStatusAsync();
}
