using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime;

namespace Cyborg.Modules.Conditions.FileExists;

[GeneratedModuleValidation]
public sealed partial record FileExistsModule
(
    [property: Required][property: Untagged] string Path
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.condition.file_exists.v1";
}
