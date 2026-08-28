using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Modules.Conditions.Not;

[GeneratedModuleValidation]
public sealed partial record NotModule
(
    [property: Required] ModuleReference Condition
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.condition.not.v1";
}
