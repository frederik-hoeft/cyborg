using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Debugging;

/// <summary>Context exposed to debug frontends when execution is paused at a module boundary.</summary>
public interface IDebugPauseContext
{
    string ModuleId { get; }

    IValidationResult<IModule> ValidationResult { get; }

    IModuleRuntime Runtime { get; }

    /// <summary>
    /// Service provider associated with the executing module. Frontends may use it as the fallback provider for dispatch-local command dependency injection.
    /// </summary>
    IServiceProvider Services { get; }

    IBreakpointRegistry Breakpoints { get; }

    /// <summary>Diagnostics associated with entering the current pause, such as breakpoint evaluation failures.</summary>
    IReadOnlyList<DebugDiagnostic> Diagnostics { get; }

    /// <summary>Requests a one-shot break at the next module executed through the runtime (step).</summary>
    void RequestStep();

    /// <summary>Removes all breakpoints and leaves debugging inactive after the current resume.</summary>
    void Detach();
}
