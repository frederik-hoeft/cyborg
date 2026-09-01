using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Services.Debugging;

/// <summary>
/// Core debugging service consulted by the module execution pipeline after preparation/constraint evaluation and before validation is enforced for execution.
/// </summary>
public interface IWorkflowDebugger
{
    /// <summary>
    /// Evaluates global breakpoints and branch-local debugger control for the prepared validation result before execution enforces validity.
    /// Returns <see cref="DebugResumeAction.Continue"/> immediately when the current branch does not need to pause.
    /// </summary>
    ValueTask<DebugResumeAction> EvaluatePreExecutionAsync(
        string moduleId,
        IValidationResult<IModule> validationResult,
        IModuleRuntime runtime,
        IServiceProvider services,
        CancellationToken cancellationToken);
}
