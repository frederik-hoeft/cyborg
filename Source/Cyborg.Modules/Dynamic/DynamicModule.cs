using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.ComponentModel.DataAnnotations;

namespace Cyborg.Modules.Dynamic;

[GeneratedModuleValidation]
public sealed partial record DynamicModule
(
    [property: Required] ModuleContext Body
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.dynamic.v1";
}