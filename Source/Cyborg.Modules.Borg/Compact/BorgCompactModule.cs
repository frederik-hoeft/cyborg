using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;

namespace Cyborg.Modules.Borg.Compact;

[GeneratedModuleValidation]
public sealed partial record BorgCompactModule
(
    [property: DefaultValue<int>(10)][property: Range<int>(Min = 1, Max = 99)] int Threshold
) : BorgModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.borg.compact.v1.4";
}