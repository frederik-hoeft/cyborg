using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.Collections.Immutable;

namespace Cyborg.Modules.Switch;

[GeneratedModuleValidation]
public sealed partial record SwitchModule
(
    [property: Required] string Variable,
    [property: MinLength(1)] ImmutableArray<SwitchReference> Cases
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.switch.v1";
}

[Validatable]
public sealed record SwitchReference
(
    [property: Required] string Name,
    [property: Required] string Path
);