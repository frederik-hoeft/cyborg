using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Network.WakeOnLan;

public sealed record WakeOnLanModule
(
    string TargetHost,
    string MacAddress,
    int LivenessProbePort,
    string StateVariable,
    TimeSpan MaxWaitTime,
    TimeSpan HostDiscoveryTimeout,
    ModuleEnvironmentReference? OutputEnvironment,
    string Executable = "/usr/bin/wakeonlan"
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.network.wol.v1";
}