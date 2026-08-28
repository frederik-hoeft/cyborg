using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Modules.Parallel;

[GeneratedModuleValidation]
public sealed partial record ParallelModule([property: MinLength(1)] IReadOnlyList<ModuleContext> Branches) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.parallel.v1";
}
