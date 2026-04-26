using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;

namespace Cyborg.Modules.Glob;

[GeneratedModuleValidation]
public sealed partial record GlobModule
(
    [property: Required] string Pattern,
    [property: Required][property: DirectoryExists] string Root,
    [property: DefaultValue<bool>(false)] bool Recurse
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.glob.v1";
}
