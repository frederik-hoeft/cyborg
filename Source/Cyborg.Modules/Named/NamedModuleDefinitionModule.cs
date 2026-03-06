using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Named;

public sealed record NamedModuleDefinitionModule
(
    string Name,
    ModuleReference Module,
    ModuleEnvironment? Environment,
    ModuleReference? Configuration
) : ModuleContext(Module, Environment, Configuration), IModule
{
    public static string ModuleId => "cyborg.modules.named.v1";
}
