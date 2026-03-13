using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.Text.RegularExpressions;

namespace Cyborg.Modules.Network.WakeOnLan;

[GeneratedModuleValidation]
public sealed partial record WakeOnLanModule
(
    [property: Required] string TargetHost,
    [property: Required][property: MustMatch(nameof(WakeOnLanModule.MacAddressRegex))] string MacAddress,
    [property: Required][property: Range<int>(Min = 1, Max = ushort.MaxValue)] int LivenessProbePort,
    [property: DefaultTimeSpan("00:05:00")] TimeSpan MaxWaitTime,
    [property: DefaultTimeSpan("00:00:30")] TimeSpan HostDiscoveryTimeout,
    [property: Required][property: DefaultValue<string>("/usr/bin/wakeonlan")][property: FileExists] string Executable
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.network.wol.v1";

    [GeneratedRegex(@"^([0-9A-Fa-f]{2}[:\-]){5}([0-9A-Fa-f]{2})$")]
    private static partial Regex MacAddressRegex { get; }
}