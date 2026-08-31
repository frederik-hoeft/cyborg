namespace Cyborg.Core.Runtime.Hooks;

/// <summary>Observes the complete structured lifetime of runtime-owned module invocations.</summary>
/// <remarks>
/// Execution lifecycle hooks are observers. The runtime isolates hook failures so observers cannot change
/// workflow execution, reconciliation, or delivery to later hooks.
/// </remarks>
public interface IModuleExecutionLifecycleHook : IModuleLifecycleHook
{
    ValueTask OnStartedAsync(IModuleExecutionStartedContext context, CancellationToken cancellationToken);

    ValueTask OnCompletedAsync(IModuleExecutionCompletedContext context, CancellationToken cancellationToken);

    ValueTask OnClosedAsync(IModuleExecutionClosedContext context, CancellationToken cancellationToken);
}
