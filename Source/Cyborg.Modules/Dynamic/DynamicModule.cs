using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Modules.Dynamic;

[GeneratedModuleValidation]
public sealed partial record DynamicModule
(
    [property: Required] ModuleContext Target,
    [property: VariableIdentifier(TargetsElements = true)][property: Required(TargetsElements = true)] IReadOnlyCollection<string>? Tags
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.dynamic.v1";
}
