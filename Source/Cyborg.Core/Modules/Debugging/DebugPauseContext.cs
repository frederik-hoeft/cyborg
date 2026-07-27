using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Debugging;

// TODO: can probably be a record
internal sealed class DebugPauseContext(
    IModule module,
    string moduleId,
    IModuleRuntime runtime,
    IBreakpointRegistry breakpoints,
    Action requestStep,
    Action detach) : IDebugPauseContext
{
    public IModule Module { get; } = module;

    public string ModuleId { get; } = moduleId;

    public string ModuleIdentity { get; } = Debugging.ModuleIdentity.Format(module, moduleId);

    public IModuleRuntime Runtime { get; } = runtime;

    public IBreakpointRegistry Breakpoints { get; } = breakpoints;

    public string Inspect()
    {
        if (Module is IInspectable inspectable)
        {
            return inspectable.Inspect();
        }

        // Fallback for modules without generated inspection support.
        return ModuleIdentity;
    }

    public void RequestStep() => requestStep();

    public void Detach() => detach();
}
