using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.Collections.Immutable;

namespace Cyborg.Modules.Sequence;

[GeneratedModuleValidation]
public sealed partial record SequenceModule([property: MinLength(1)] ImmutableArray<ModuleContext> Steps) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.sequence.v1";
}