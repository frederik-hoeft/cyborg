using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Modules.If;

namespace Cyborg.Modules.Conditions.Or;

[GeneratedModuleValidation]
public sealed partial record OrModule
(
    [property: Required][property: MinLength(1)] IReadOnlyCollection<ModuleReference> Conditions
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.condition.or.v1";
}