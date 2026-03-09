using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;

namespace Cyborg.Modules.Network.SshShutdown;

[GeneratedModuleValidation]
public sealed partial record SshShutdownModule
(
    [property: Required]
    string Hostname,
    [property: Required][property: Range<int>(Min = 1, Max = ushort.MaxValue)]
    int Port
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.network.ssh_shutdown.v1";
}