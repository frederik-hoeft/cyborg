using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.External;

[GeneratedModuleValidation]
public sealed partial record ExternalModule
(
    [property: Required][property: FileExists] string Path
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.external.v1";
}