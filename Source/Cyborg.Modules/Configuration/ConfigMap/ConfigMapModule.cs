using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.Collections.Immutable;

namespace Cyborg.Modules.Configuration.ConfigMap;

[GeneratedModuleValidation]
public sealed partial record ConfigMapModule
(
    [property: MinLength(1)] ImmutableArray<DynamicKeyValuePair> Entries
) : ModuleBase, IConfigurationModule
{
    public static string ModuleId => "cyborg.modules.config.map.v1";
}