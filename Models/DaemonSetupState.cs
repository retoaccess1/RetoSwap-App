namespace Manta.Models;

public enum DaemonSetupState
{
    Initial,
    InstallingDependencies,
    ExtractingRootfs,
    Finished
}