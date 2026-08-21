namespace Cyborg.Core.Services.Dispatch;

public interface IChildProcessDispatcher
{
    /// <summary>
    /// Executes a metadata-aware invocation. Implementations preserve tagged values until the raw
    /// process execution boundary and render diagnostics safely.
    /// </summary>
    Task<ChildProcessResult> ExecuteAsync(ChildProcessInvocation invocation, CancellationToken cancellationToken);
}
