using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Modules.Configuration.ConfigCollection;

[GeneratedModuleValidation]
public sealed partial record ConfigCollectionModule
(
    [property: Required][property: MinLength(1)] IReadOnlyCollection<ModuleReference> Sources
) : ModuleBase, IConfigurationModule
{
    public static string ModuleId => "cyborg.modules.config.collection.v1";
}
