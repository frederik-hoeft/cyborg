namespace Cyborg.Core.Modules.Runtime.Transactions.Services;

/// <summary>
/// Describes one DI service state component that participates in Cyborg execution transactions.
/// </summary>
/// <remarks>
/// Participant descriptors are application-level services. They define state semantics but must not hold
/// mutable workflow state themselves; state belongs to individual execution transactions.
/// </remarks>
public abstract class TransactionalServiceParticipant
{
    private protected TransactionalServiceParticipant()
    {
    }

    internal abstract object CreateRootStateCore();

    internal abstract ITransactionalServiceForkAdapter CreateForkCore(object ownerState);
}

/// <summary>
/// Typed participant descriptor for a transaction-aware DI service.
/// </summary>
/// <typeparam name="TState">The participant-owned transaction state type.</typeparam>
public abstract class TransactionalServiceParticipant<TState> : TransactionalServiceParticipant where TState : class
{
    protected TransactionalServiceParticipant()
    {
    }

    /// <summary>
    /// Creates the initial state for an independent root execution.
    /// </summary>
    /// <remarks>
    /// Each call must return state owned by that root execution. Application-level immutable configuration may be
    /// shared through the participant descriptor, but mutable workflow state must not be shared between roots.
    /// </remarks>
    protected abstract TState CreateRootState();

    /// <summary>
    /// Captures a stable fork point for the supplied owner state.
    /// </summary>
    /// <remarks>
    /// The returned fork must preserve the effective owner state at this point even if later reconciliation replaces
    /// the owner transaction's published component state.
    /// </remarks>
    protected abstract TransactionalServiceFork<TState> CreateFork(TState ownerState);

    internal sealed override object CreateRootStateCore() =>
        CreateRootState() ?? throw new InvalidOperationException($"Transactional service participant '{GetType().FullName}' returned a null root state.");

    internal sealed override ITransactionalServiceForkAdapter CreateForkCore(object ownerState)
    {
        ArgumentNullException.ThrowIfNull(ownerState);
        if (ownerState is not TState typedOwnerState)
        {
            throw new InvalidOperationException(
                $"Transactional service participant '{GetType().FullName}' expected state type '{typeof(TState).FullName}' but received '{ownerState.GetType().FullName}'.");
        }
        TransactionalServiceFork<TState> fork = CreateFork(typedOwnerState)
            ?? throw new InvalidOperationException($"Transactional service participant '{GetType().FullName}' returned a null fork.");
        return new TransactionalServiceForkAdapter<TState>(fork);
    }
}
