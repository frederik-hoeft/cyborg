using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Modules;
using System.ComponentModel.DataAnnotations;

namespace Cyborg.Modules.If.Conditions.IsSet;

[GeneratedModuleValidation]
public sealed partial record IsSetModule
(
    [property: Required] string Variable
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.if.condition.is_set.v1";
}