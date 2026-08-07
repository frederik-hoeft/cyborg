using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Debugging;

/// <summary>
/// Context exposed to debug frontends when execution is paused at a module boundary.
/// Intentionally free of console I/O so remote or web adapters can reuse it.
/// </summary>
public interface IDebugPauseContext
{
    IModule Module { get; }

    string ModuleId { get; }

    /// <summary>
    /// Short identity representation of the current module (id/name/group).
    /// </summary>
    string ModuleIdentity { get; }

    IModuleRuntime Runtime { get; }

    IBreakpointRegistry Breakpoints { get; }

    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Requests a one-shot break at the next module executed through the runtime (step).
    /// Implemented by registering a <c>.*</c> one-shot breakpoint so step and break share matching logic.
    /// </summary>
    void RequestStep();

    /// <summary>
    /// Removes all breakpoints and leaves debugging inactive after the current resume.
    /// </summary>
    void Detach();
}
