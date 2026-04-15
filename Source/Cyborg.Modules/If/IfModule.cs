using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.If;

[GeneratedModuleValidation]
public sealed partial record IfModule
(
    [property: Required] ModuleReference Condition,
    [property: Required] ModuleContext Then,
    ModuleContext? Else = null,
    bool InvertCondition = false
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.if.v1";
}