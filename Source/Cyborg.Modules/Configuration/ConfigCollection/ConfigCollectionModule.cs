using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.Collections.Immutable;

namespace Cyborg.Modules.Configuration.ConfigCollection;

[GeneratedModuleValidation]
public sealed partial record ConfigCollectionModule
(
    [property: MinLength(1)] ImmutableArray<ModuleReference> Sources
) : ModuleBase, IConfigurationModule
{
    public static string ModuleId => "cyborg.modules.config.collection.v1";
}