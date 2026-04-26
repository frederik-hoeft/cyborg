using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Modules;
using System.ComponentModel.DataAnnotations;

namespace Cyborg.Modules.Conditions.IsSet;

[GeneratedModuleValidation]
public sealed partial record IsSetModule
(
    [property: Required] string Variable
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.condition.is_set.v1";
}
