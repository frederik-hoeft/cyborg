using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;

namespace Cyborg.Modules.Conditions.DirectoryExists;

[GeneratedModuleValidation]
public sealed partial record DirectoryExistsModule
(
    [property: Required] string Path
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.condition.directory_exists.v1";
}
