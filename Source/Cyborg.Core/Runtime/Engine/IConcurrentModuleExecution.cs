namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// One child invocation started inside an <see cref="IConcurrentExecutionScope"/>.
/// </summary>
public interface IConcurrentModuleExecution
{
    /// <summary>
    /// Completes when the child has a definite execution result. The owning scope stays open and unreconciled.
    /// </summary>
    Task<IModuleExecutionResult> Completion { get; }

    /// <summary>
    /// Requests cancellation of this child only. Siblings and the owning invocation keep running.
    /// </summary>
    void Cancel();
}
