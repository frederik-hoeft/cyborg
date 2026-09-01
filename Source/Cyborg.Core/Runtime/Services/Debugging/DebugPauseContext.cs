using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed record DebugPauseContext
(
    string ModuleId,
    IValidationResult<IModule> ValidationResult,
    IModuleRuntime Runtime,
    IServiceProvider Services,
    IBreakpointRegistry Breakpoints,
    IReadOnlyList<DebugDiagnostic> Diagnostics
) : IDebugPauseContext;
