using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Services.Default;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Debugging;

internal sealed class WorkflowDebugger(IBreakpointRegistry breakpoints, ILoggerFactory loggerFactory, IDefault<IDebugFrontend> defaultFrontend) : IWorkflowDebugger
{
    /// <summary>Expression used to implement <c>step</c> via the shared breakpoint matcher.</summary>
    public const string STEP_EXPRESSION = ".*";

    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.debugging");

    public bool IsEnabled => breakpoints.Count > 0;

    public async ValueTask<DebugResumeAction> EvaluatePreExecutionAsync(string moduleId, IValidationResult<IModule> validationResult, IModuleRuntime runtime,
        IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(validationResult);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(services);

        // skip evaluation if debugging is disabled or we're running headless (no frontend to handle the breakpoint)
        if (!IsEnabled || defaultFrontend.GetDefault() is not { } frontend)
        {
            return DebugResumeAction.Continue;
        }

        IModule module = validationResult.Module;
        BreakpointContext context = new(moduleId, module.Name, module.Group);
        if (!breakpoints.TryMatchAndConsume(in context, out BreakpointExpression? matched))
        {
            return DebugResumeAction.Continue;
        }

        cancellationToken.ThrowIfCancellationRequested();
        DebugPauseContext pauseContext = new(
            moduleId,
            validationResult,
            runtime,
            services,
            breakpoints,
            RequestStepAction: () => breakpoints.Add(STEP_EXPRESSION, isOneShot: true),
            DetachAction: breakpoints.Clear);

        _logger.LogBreakpointHit(pauseContext.GetModuleIdentity(), matched.Expression);

        return await frontend.PauseAsync(pauseContext, cancellationToken).ConfigureAwait(false);
    }
}
