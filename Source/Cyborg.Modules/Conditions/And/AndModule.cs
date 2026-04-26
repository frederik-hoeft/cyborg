using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Conditions.And;

[GeneratedModuleValidation]
public sealed partial record AndModule
(
    [property: Required][property: MinLength(1)] IReadOnlyCollection<ModuleReference> Conditions
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.condition.and.v1";
}
