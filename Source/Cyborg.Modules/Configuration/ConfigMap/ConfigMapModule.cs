using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Configuration.ConfigMap;

[GeneratedModuleValidation]
public sealed partial record ConfigMapModule
(
    [property: Required][property: MinLength(1)] IReadOnlyCollection<DynamicKeyValuePair> Entries
) : ModuleBase, IConfigurationModule
{
    public static string ModuleId => "cyborg.modules.config.map.v1";
}