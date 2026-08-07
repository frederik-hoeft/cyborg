using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Debugging;

/// <summary>
/// Core debugging service consulted by the module execution pipeline at module boundaries.
/// When inactive, the runtime takes a cheap early-out path with no caller-observable behavior change.
/// </summary>
public interface IWorkflowDebugger
{
    /// <summary>
    /// True when at least one breakpoint is registered or a step is pending.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Evaluates breakpoints for the module about to execute (after load/init/validation).
    /// Returns <see cref="DebugResumeAction.Continue"/> immediately when no breakpoint matches.
    /// </summary>
    ValueTask<DebugResumeAction> EvaluatePreExecutionAsync(IModule module, string moduleId, IModuleRuntime runtime, IServiceProvider services, CancellationToken cancellationToken);
}
