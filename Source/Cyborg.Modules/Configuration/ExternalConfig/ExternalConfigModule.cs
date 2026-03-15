using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Configuration.ExternalConfig;

[GeneratedModuleValidation]
public sealed partial record ExternalConfigModule
(
    [property: Required][property: FileExists] string Path
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.config.external.v1";
}