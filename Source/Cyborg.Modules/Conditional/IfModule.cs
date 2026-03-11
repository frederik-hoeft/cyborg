using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.ComponentModel.DataAnnotations;

namespace Cyborg.Modules.Conditional;

[GeneratedModuleValidation]
public sealed partial record IfModule
(
    [property: Required] ModuleReference Condition,
    [property: Required] ModuleContext Then,
    ModuleContext? Else = null
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.if.v1";
}