using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Model;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Named;

[GeneratedModuleValidation]
public sealed partial record NamedModuleReferenceModule([property: Required][property: MinLength(1)] string Target) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.named.ref.v1";
}