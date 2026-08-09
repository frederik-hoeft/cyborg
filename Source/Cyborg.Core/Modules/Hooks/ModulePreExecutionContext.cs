using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Hooks;

internal sealed record ModulePreExecutionContext(string ModuleId, IValidationResult<IModule> ValidationResult, IModuleRuntime Runtime, IModuleResultBuilder ResultBuilder) : IModulePreExecutionContext;
