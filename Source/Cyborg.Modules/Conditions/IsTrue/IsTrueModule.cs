using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime;

namespace Cyborg.Modules.Conditions.IsTrue;

[GeneratedModuleValidation]
public sealed partial record IsTrueModule
(
    [property: Required][property: Untagged] string Variable
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.condition.is_true.v1";
}
