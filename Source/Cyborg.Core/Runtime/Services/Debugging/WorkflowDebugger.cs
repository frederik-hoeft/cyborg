using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Runtime.Services.Validation;
using Cyborg.Core.Services.Default;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class WorkflowDebugger(
    IBreakpointRegistry breakpoints,
    ILoggerFactory loggerFactory,
    IDefault<IDebugFrontend> defaultFrontend,
    IDebugExecutionTopology topology,
    IDebugSessionState sessionState) : IWorkflowDebugger
{
    private readonly DebugPauseCoordinator _pauseCoordinator = new(topology, sessionState);
    private readonly IDebugSessionStateController _sessionState =
        sessionState as IDebugSessionStateController
        ?? throw new ArgumentException("The debugger session service must expose controller operations.", nameof(sessionState));
    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.debugging");

    public async ValueTask<DebugResumeAction> EvaluatePreExecutionAsync(
        string moduleId,
        IValidationResult<IModule> validationResult,
        IModuleRuntime runtime,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(validationResult);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(services);

        IDebugBranchControl branchControl = services.GetRequiredService<IDebugBranchControl>();
        bool stepping = branchControl.IsStepping;
        if (!stepping && breakpoints.Count == 0)
        {
            return DebugResumeAction.Continue;
        }

        IModule module = validationResult.Module;
        BreakpointEvaluationResult evaluationResult = BreakpointEvaluationResult.NoMatch;
        if (breakpoints.Count > 0)
        {
            BreakpointContext breakpointContext = new(moduleId, module.Name, module.Group);
            evaluationResult = breakpoints.EvaluateAndConsume(in breakpointContext);
        }

        if (!stepping && !evaluationResult.ShouldPause)
        {
            return DebugResumeAction.Continue;
        }

        if (defaultFrontend.GetDefault() is not { } frontend)
        {
            return DebugResumeAction.Continue;
        }

        cancellationToken.ThrowIfCancellationRequested();
        long sessionGeneration = _sessionState.Generation;
        ModuleExecutionId? executionId = (runtime as IModuleExecutionRuntime)?.InvocationContext?.ExecutionId;
        using DebugPauseLease? pauseLease = await _pauseCoordinator
            .AcquireAsync(executionId, sessionGeneration, cancellationToken)
            .ConfigureAwait(false);
        if (pauseLease is null)
        {
            return DebugResumeAction.Continue;
        }

        // Detach may invalidate the session while this pause is queued. Re-check after frontend
        // ownership is granted so a stale decided pause cannot enter the frontend.
        if (_sessionState.Generation != sessionGeneration)
        {
            return DebugResumeAction.Continue;
        }

        DebugPauseContext pauseContext = new(
            moduleId,
            validationResult,
            runtime,
            services,
            breakpoints,
            evaluationResult.Diagnostics);

        LogPause(pauseContext, evaluationResult, stepping);
        DebugResumeAction action = await frontend.PauseAsync(pauseContext, cancellationToken).ConfigureAwait(false);
        return ApplyResumeAction(action, branchControl);
    }

    private DebugResumeAction ApplyResumeAction(DebugResumeAction action, IDebugBranchControl branchControl)
    {
        switch (action)
        {
            case DebugResumeAction.Continue:
                branchControl.Continue();
                return DebugResumeAction.Continue;
            case DebugResumeAction.Step:
                branchControl.Step();
                return DebugResumeAction.Continue;
            case DebugResumeAction.Cancel:
                branchControl.Continue();
                return DebugResumeAction.Cancel;
            case DebugResumeAction.Detach:
                breakpoints.Clear();
                _sessionState.Invalidate();
                branchControl.Continue();
                return DebugResumeAction.Continue;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown debugger resume action.");
        }
    }

    private void LogPause(DebugPauseContext pauseContext, BreakpointEvaluationResult evaluationResult, bool stepping)
    {
        if (evaluationResult.Status is BreakpointEvaluationStatus.Match)
        {
            _logger.LogBreakpointHit(pauseContext.GetModuleIdentity(), evaluationResult.Breakpoint!.Expression);
            return;
        }

        if (evaluationResult.Status is BreakpointEvaluationStatus.Faulted)
        {
            foreach (DebugDiagnostic diagnostic in evaluationResult.Diagnostics)
            {
                _logger.LogBreakpointEvaluationFailed(
                    pauseContext.GetModuleIdentity(),
                    evaluationResult.Breakpoint!.Expression,
                    diagnostic.Message);
            }
            return;
        }

        if (stepping)
        {
            _logger.LogStepPause(pauseContext.GetModuleIdentity());
        }
    }
}
