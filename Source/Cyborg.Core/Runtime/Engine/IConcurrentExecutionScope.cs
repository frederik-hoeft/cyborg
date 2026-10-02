using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// Structured concurrent execution of child module invocations on one fork.
/// The owning invocation continues on the fork continuation until the scope is closed or disposed.
/// </summary>
public interface IConcurrentExecutionScope : IAsyncDisposable
{
    /// <summary>
    /// Starts one nested module invocation. Its <see cref="IConcurrentModuleExecution.Completion"/> task can be observed before <see cref="CloseAsync"/>.
    /// A structural failure while establishing the child aborts this scope because the fork can no longer be reconciled normally.
    /// </summary>
    ValueTask<IConcurrentModuleExecution> StartAsync(ModuleContext moduleContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits until every started child has terminated, then reconciles the continuation with those children.
    /// </summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
