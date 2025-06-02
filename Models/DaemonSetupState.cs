namespace Manta.Models;

public enum DaemonSetupState
{
    Initial,
    InstallingDependencies,
    InstallingHavenoDaemon,
    Finished
}