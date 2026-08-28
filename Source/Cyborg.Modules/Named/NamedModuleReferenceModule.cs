using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime;

namespace Cyborg.Modules.Named;

[GeneratedModuleValidation]
public sealed partial record NamedModuleReferenceModule([property: Required][property: Untagged] string Target) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.named.ref.v1";
}
