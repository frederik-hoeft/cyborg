namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// One child invocation started inside an <see cref="IConcurrentExecutionScope"/>.
/// </summary>
public interface IConcurrentModuleExecution
{
    /// <summary>
    /// Completes when the child has a definite execution result. The task faults if structural execution fails before a result exists.
    /// The owning scope stays open and unreconciled in either case.
    /// </summary>
    Task<IModuleExecutionResult> Completion { get; }

    /// <summary>
    /// Requests cancellation of this child only. Siblings and the owning invocation keep running.
    /// </summary>
    void Cancel();
}
