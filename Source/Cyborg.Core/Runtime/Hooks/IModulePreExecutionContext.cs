using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Hooks;

public interface IModulePreExecutionContext
{
    string ModuleId { get; }

    IValidationResult<IModule> ValidationResult { get; }

    IModuleRuntime Runtime { get; }

    IModuleResultBuilder ResultBuilder { get; }
}
