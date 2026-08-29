using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Hooks;

internal sealed record ModulePreExecutionContext(string ModuleId, IValidationResult<IModule> ValidationResult, IModuleRuntime Runtime, IModuleResultBuilder ResultBuilder) : IModulePreExecutionContext;
