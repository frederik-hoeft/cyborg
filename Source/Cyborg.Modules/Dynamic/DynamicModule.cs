using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Dynamic;

[GeneratedModuleValidation]
public sealed partial record DynamicModule
(
    [property: Required] ModuleContext Target,
    IReadOnlyCollection<string>? Tags
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.dynamic.v1";
}