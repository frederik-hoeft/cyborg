using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Configuration.ExternalConfig;

[GeneratedModuleValidation]
public sealed partial record ExternalConfigModule
(
    [property: Required][property: FileExists] string Path
) : ModuleBase, IConfigurationModule
{
    public static string ModuleId => "cyborg.modules.config.external.v1";
}