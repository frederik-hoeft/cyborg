using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Debugging;

internal sealed record DebugPauseContext
(
    string ModuleId,
    IValidationResult<IModule> ValidationResult,
    IModuleRuntime Runtime,
    IServiceProvider Services,
    IBreakpointRegistry Breakpoints,
    IReadOnlyList<DebugDiagnostic> Diagnostics,
    Action RequestStepAction,
    Action DetachAction
) : IDebugPauseContext
{
    public void RequestStep() => RequestStepAction();

    public void Detach() => DetachAction();
}
