using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;

namespace Cyborg.Modules.If.Conditions.DirectoryExists;

[GeneratedModuleValidation]
public sealed partial record DirectoryExistsModule
(
    [property: Required] string Path
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.if.condition.directory_exists.v1";
}