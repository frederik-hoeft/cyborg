using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Hooks;

public interface IModulePreExecutionContext
{
    string ModuleId { get; }

    IValidationResult<IModule> ValidationResult { get; }

    IModuleRuntime Runtime { get; }

    IModuleResultBuilder ResultBuilder { get; }
}
