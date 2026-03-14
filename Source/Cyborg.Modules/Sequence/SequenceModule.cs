using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Sequence;

[GeneratedModuleValidation]
public sealed partial record SequenceModule([property: MinLength(1)] IReadOnlyCollection<ModuleContext> Steps) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.sequence.v1";
}