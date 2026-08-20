using System.Diagnostics;

namespace Cyborg.Core.Services.Dispatch;

public interface IChildProcessDispatcher
{
    /// <summary>
    /// Executes a metadata-aware invocation. Implementations should preserve tagged values until
    /// the raw process execution boundary and render diagnostics safely.
    /// </summary>
    Task<ChildProcessResult> ExecuteAsync(ChildProcessInvocation invocation, CancellationToken cancellationToken) =>
        ExecuteAsync(invocation.CreateProcessStartInfo(), cancellationToken);

    /// <summary>
    /// Executes an already-materialized process start. Metadata associated with arguments or
    /// environment values is unavailable through this compatibility API.
    /// </summary>
    Task<ChildProcessResult> ExecuteAsync(ProcessStartInfo processStartInfo, CancellationToken cancellationToken);
}
