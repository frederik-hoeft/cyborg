using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Foreach;

[GeneratedModuleValidation]
public sealed partial record ForeachModule
(
    [property: Required] string Collection,
    [property: Required] string ItemVariable,
    [property: DefaultValue<bool>(false)] bool ContinueOnError,
    [property: Required] ModuleContext Body
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.foreach.v1";
}
