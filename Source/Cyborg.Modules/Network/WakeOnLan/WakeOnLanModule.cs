using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Modules.Shared.Model;

namespace Cyborg.Modules.Network.WakeOnLan;

[GeneratedModuleValidation]
public sealed partial record WakeOnLanModule
(
    [property: Required] string TargetHost,
    [property: Required][property: ExactLength(17)] string MacAddress,
    [property: Required][property: Range<int>(Min = 1, Max = ushort.MaxValue)] int LivenessProbePort,
    [property: Required] string StateVariable,
    [property: DefaultTimeSpan("00:05:00")] TimeSpan MaxWaitTime,
    [property: DefaultTimeSpan("00:00:30")] TimeSpan HostDiscoveryTimeout,
    ModuleEnvironmentReference? OutputEnvironment,
    [property: Required][property: IgnoreOverrides][property: DefaultValue<string>("/usr/bin/wakeonlan")] string Executable
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.network.wol.v1";
}