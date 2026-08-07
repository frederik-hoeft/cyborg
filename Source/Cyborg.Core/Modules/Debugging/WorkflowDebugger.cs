using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Default;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Debugging;

internal sealed class WorkflowDebugger(IBreakpointRegistry breakpoints, ILoggerFactory loggerFactory, IDefault<IDebugFrontend> defaultFrontend) : IWorkflowDebugger
{
    /// <summary>Expression used to implement <c>step</c> via the shared breakpoint matcher.</summary>
    public const string STEP_EXPRESSION = ".*";

    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.debugging");

    public bool IsEnabled => breakpoints.Count > 0;

    public async ValueTask<DebugResumeAction> EvaluatePreExecutionAsync(IModule module, string moduleId, IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(runtime);

        if (!IsEnabled)
        {
            return DebugResumeAction.Continue;
        }

        BreakpointContext context = new(moduleId, module.Name, module.Group);
        if (!breakpoints.TryMatchAndConsume(in context, out BreakpointExpression? matched))
        {
            return DebugResumeAction.Continue;
        }

        cancellationToken.ThrowIfCancellationRequested();
        DebugPauseContext pauseContext = new(module, moduleId, runtime, breakpoints, RequestStepAction: () => breakpoints.Add(STEP_EXPRESSION, isOneShot: true), DetachAction: breakpoints.Clear);

        _logger.LogBreakpointHit(pauseContext.ModuleIdentity, matched!.Expression);

        IDebugFrontend frontend = defaultFrontend.GetRequiredDefault();
        return await frontend.PauseAsync(pauseContext, cancellationToken).ConfigureAwait(false);
    }
}
