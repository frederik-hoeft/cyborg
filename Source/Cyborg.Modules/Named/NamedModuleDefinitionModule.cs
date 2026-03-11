using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Named;

[GeneratedModuleValidation]
public sealed partial record NamedModuleDefinitionModule
(
    [property: Required][property: MinLength(1)] string Name,
    ModuleReference Module,
    ModuleEnvironment? Environment,
    ModuleReference? Configuration
) : ModuleContext(Module, Environment, Configuration), IModule
{
    public static string ModuleId => "cyborg.modules.named.v1";
}
