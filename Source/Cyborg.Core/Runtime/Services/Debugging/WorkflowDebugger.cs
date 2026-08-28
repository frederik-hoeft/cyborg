using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Runtime.Services.Validation;
using Cyborg.Core.Services.Default;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class WorkflowDebugger(IBreakpointRegistry breakpoints, ILoggerFactory loggerFactory, IDefault<IDebugFrontend> defaultFrontend) : IWorkflowDebugger
{
    /// <summary>Expression used to implement <c>step</c> via the shared breakpoint matcher.</summary>
    public const string STEP_EXPRESSION = ".*";

    private readonly SemaphoreSlim _evaluationGate = new(initialCount: 1, maxCount: 1);
    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.debugging");

    public bool IsEnabled => breakpoints.Count > 0;

    public async ValueTask<DebugResumeAction> EvaluatePreExecutionAsync(string moduleId, IValidationResult<IModule> validationResult, IModuleRuntime runtime,
        IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(validationResult);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(services);

        if (!IsEnabled)
        {
            return DebugResumeAction.Continue;
        }

        await _evaluationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check after acquiring the gate because a preceding pause can detach or consume the last one-shot breakpoint.
            if (!IsEnabled || defaultFrontend.GetDefault() is not { } frontend)
            {
                return DebugResumeAction.Continue;
            }

            IModule module = validationResult.Module;
            BreakpointContext context = new(moduleId, module.Name, module.Group);
            BreakpointEvaluationResult evaluationResult = breakpoints.EvaluateAndConsume(in context);
            if (!evaluationResult.ShouldPause || evaluationResult.Breakpoint is not { } breakpoint)
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
                evaluationResult.Diagnostics,
                RequestStepAction: () => breakpoints.Add(STEP_EXPRESSION, isOneShot: true),
                DetachAction: breakpoints.Clear);

            if (evaluationResult.Status is BreakpointEvaluationStatus.Match)
            {
                _logger.LogBreakpointHit(pauseContext.GetModuleIdentity(), breakpoint.Expression);
            }
            else
            {
                foreach (DebugDiagnostic diagnostic in evaluationResult.Diagnostics)
                {
                    _logger.LogBreakpointEvaluationFailed(pauseContext.GetModuleIdentity(), breakpoint.Expression, diagnostic.Message);
                }
            }

            return await frontend.PauseAsync(pauseContext, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _evaluationGate.Release();
        }
    }
}
