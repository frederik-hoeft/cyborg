using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Modules.Retry;

[GeneratedModuleValidation]
public sealed partial record RetryModule(
    [property: Required][property: Range<int>(Min = 1, Max = MAX_ATTEMPTS)] int Attempts,
    [property: Required] ModuleContext Body
) : ModuleBase, IModule
{
    public const int MAX_ATTEMPTS = 1000;

    public static string ModuleId => "cyborg.modules.retry.v1";
}
