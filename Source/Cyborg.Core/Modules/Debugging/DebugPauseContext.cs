using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Debugging;

internal sealed record DebugPauseContext(
    IModule Module,
    string ModuleId,
    IModuleRuntime Runtime,
    IServiceProvider Services,
    IBreakpointRegistry Breakpoints,
    Action RequestStepAction,
    Action DetachAction) : IDebugPauseContext
{
    public string ModuleIdentity { get; } = Debugging.ModuleIdentity.Format(Module, ModuleId);

    public void RequestStep() => RequestStepAction();

    public void Detach() => DetachAction();
}
