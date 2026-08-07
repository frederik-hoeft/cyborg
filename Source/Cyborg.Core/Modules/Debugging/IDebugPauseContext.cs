using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Debugging;

/// <summary>Context exposed to debug frontends when execution is paused at a module boundary.</summary>
public interface IDebugPauseContext
{
    IModule Module { get; }

    string ModuleId { get; }

    /// <summary>Short identity representation of the current module (id/name/group).</summary>
    string ModuleIdentity { get; }

    IModuleRuntime Runtime { get; }

    IBreakpointRegistry Breakpoints { get; }

    /// <summary>Requests a one-shot break at the next module executed through the runtime (step).</summary>
    void RequestStep();

    /// <summary>Removes all breakpoints and leaves debugging inactive after the current resume.</summary>
    void Detach();
}
