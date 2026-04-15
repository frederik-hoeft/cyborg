using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.EnvironmentDefinitions;

[GeneratedModuleValidation]
public sealed partial record EnvironmentDefinitionsModule
(
    [property: Required][property: MinLength(1)] IReadOnlyCollection<ModuleEnvironment> Environments
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.environment.defs.v1";
}