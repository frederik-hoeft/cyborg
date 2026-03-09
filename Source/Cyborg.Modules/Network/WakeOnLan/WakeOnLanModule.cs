using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Network.WakeOnLan;

[GeneratedModuleValidation]
public sealed partial record WakeOnLanModule
(
    [property: Required] string TargetHost,
    [property: Required] string MacAddress,
    [property: Range<int>(Min = 1, Max = ushort.MaxValue)] int LivenessProbePort,
    [property: Required] string StateVariable,
    TimeSpan MaxWaitTime,
    TimeSpan HostDiscoveryTimeout,
    ModuleEnvironmentReference? OutputEnvironment,
    [property: Required][property: IgnoreOverrides] string Executable = "/usr/bin/wakeonlan"
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.network.wol.v1";
}