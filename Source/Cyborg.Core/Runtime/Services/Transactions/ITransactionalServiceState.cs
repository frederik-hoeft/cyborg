namespace Cyborg.Core.Runtime.Services.Transactions;

/// <summary>
/// Stable scoped handle for one transaction-aware service state component.
/// </summary>
/// <remarks>
/// The handle resolves the current participant state for each operation. A service may safely retain the handle
/// across nested child execution, but should not retain the raw state object passed to an operation callback.
/// </remarks>
public interface ITransactionalServiceState<TState> where TState : class
{
    /// <summary>
    /// Reads the participant state currently visible to the owning invocation transaction.
    /// </summary>
    TResult Read<TResult>(Func<TState, TResult> reader);

    /// <summary>
    /// Applies one transaction-local mutation to the participant state currently visible to the owning invocation.
    /// </summary>
    void Mutate(Action<TState> mutation);
}
