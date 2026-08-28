using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Modules.While;

[GeneratedModuleValidation]
public sealed partial record WhileModule
(
    [property: Required] ModuleReference Condition,
    [property: Required] ModuleContext Body,
    bool InvertCondition = false
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.while.v1";
}
