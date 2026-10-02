using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Modules.Sidecar;

[GeneratedModuleValidation]
public sealed partial record SidecarModule(
    [property: Required] ModuleContext Module,
    [property: Required(TargetsElements = true)] IReadOnlyList<ModuleContext>? Sidecars
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.sidecar.v1";
}
