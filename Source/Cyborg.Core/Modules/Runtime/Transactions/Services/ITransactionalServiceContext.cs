namespace Cyborg.Core.Modules.Runtime.Transactions.Services;

/// <summary>
/// Provides scoped access to custom service state owned by the current execution transaction.
/// </summary>
public interface ITransactionalServiceContext
{
    /// <summary>
    /// Creates a stable scoped handle for the registered participant type.
    /// </summary>
    /// <typeparam name="TParticipant">The concrete participant descriptor type registered with DI.</typeparam>
    /// <typeparam name="TState">The participant-owned state type.</typeparam>
    ITransactionalServiceState<TState> GetState<TParticipant, TState>()
        where TParticipant : TransactionalServiceParticipant<TState>
        where TState : class;
}
