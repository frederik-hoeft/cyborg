using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Debugging;

/// <summary>
/// Core debugging service consulted by the module execution pipeline after preparation/constraint evaluation and before validation is enforced for execution.
/// When inactive, the runtime takes a cheap early-out path with no caller-observable behavior change.
/// </summary>
public interface IWorkflowDebugger
{
    /// <summary>
    /// True when at least one breakpoint is registered or a step is pending.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Evaluates breakpoints for the prepared validation result before execution enforces validity. This allows frontends to inspect invalid prepared modules and their errors.
    /// Returns <see cref="DebugResumeAction.Continue"/> immediately when no breakpoint matches.
    /// </summary>
    ValueTask<DebugResumeAction> EvaluatePreExecutionAsync(string moduleId, IValidationResult<IModule> validationResult, IModuleRuntime runtime, IServiceProvider services, CancellationToken cancellationToken);
}
