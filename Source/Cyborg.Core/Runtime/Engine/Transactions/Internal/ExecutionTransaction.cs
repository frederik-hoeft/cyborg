namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal sealed class ExecutionTransaction
(
    TransactionCoordinator coordinator,
    ExecutionTransaction? parent,
    ExecutionTransactionForkGroup? ownerFork,
    TransactionStateBundle state
)
{
    private readonly ExecutionTransactionForkGroup? _ownerFork = ownerFork;

    private ExecutionTransactionForkGroup? _openFork;

    private TransactionStateBundle _state = state;

    public ExecutionTransaction? Parent { get; } = parent;

    public bool IsRoot => Parent is null;

    public ExecutionTransactionLifecycle Lifecycle { get; private set; } = ExecutionTransactionLifecycle.Active;

    internal bool HasOpenFork => _openFork is not null;

    public TState GetParticipantState<TState>(ITransactionParticipant<TState> participant)
        where TState : ITransactionParticipantState
    {
        EnsureStateAccessible();
        return Volatile.Read(ref _state).Get(participant);
    }

    public ExecutionTransactionForkGroup Fork()
    {
        EnsureActive();
        if (_openFork is not null)
        {
            throw new InvalidOperationException("A transaction cannot open another fork group before its current fork group closes.");
        }

        ExecutionTransactionForkGroup fork = new(this, coordinator, Volatile.Read(ref _state));
        _openFork = fork;
        return fork;
    }

    public void Complete()
    {
        EnsureActive();
        EnsureNoOpenFork();
        Lifecycle = ExecutionTransactionLifecycle.Completed;
    }

    public void Discard()
    {
        EnsureActive();
        EnsureNoOpenFork();
        Lifecycle = ExecutionTransactionLifecycle.Discarded;
    }

    internal TransactionStateBundle GetStateForReconciliation(ExecutionTransactionForkGroup ownerFork)
    {
        if (!ReferenceEquals(_ownerFork, ownerFork))
        {
            throw new InvalidOperationException("The transaction does not belong to the supplied fork group.");
        }
        if (Lifecycle != ExecutionTransactionLifecycle.Completed)
        {
            throw new InvalidOperationException("Only completed transaction branches can participate in reconciliation.");
        }
        return Volatile.Read(ref _state);
    }

    internal void PublishForkResult(ExecutionTransactionForkGroup fork, TransactionStateBundle state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureActive();
        if (!ReferenceEquals(_openFork, fork))
        {
            throw new InvalidOperationException("The supplied fork group is not active on this transaction.");
        }
        Volatile.Write(ref _state, state);
        _openFork = null;
    }

    internal void ReleaseForkWithoutPublication(ExecutionTransactionForkGroup fork)
    {
        EnsureActive();
        if (!ReferenceEquals(_openFork, fork))
        {
            throw new InvalidOperationException("The supplied fork group is not active on this transaction.");
        }
        _openFork = null;
    }

    internal void MarkJoined(ExecutionTransactionForkGroup ownerFork)
    {
        if (!ReferenceEquals(_ownerFork, ownerFork))
        {
            throw new InvalidOperationException("The transaction does not belong to the supplied fork group.");
        }
        if (Lifecycle != ExecutionTransactionLifecycle.Completed)
        {
            throw new InvalidOperationException("Only completed transaction branches can be joined.");
        }
        Lifecycle = ExecutionTransactionLifecycle.Joined;
    }

    internal void EnsureCanBeDiscardedByFork(ExecutionTransactionForkGroup ownerFork)
    {
        if (!ReferenceEquals(_ownerFork, ownerFork))
        {
            throw new InvalidOperationException("The transaction does not belong to the supplied fork group.");
        }
        if (_openFork is not null)
        {
            throw new InvalidOperationException("A transaction branch with an open nested fork cannot be discarded by its owner.");
        }
        if (Lifecycle is not ExecutionTransactionLifecycle.Active
            and not ExecutionTransactionLifecycle.Completed
            and not ExecutionTransactionLifecycle.Discarded)
        {
            throw new InvalidOperationException("Only active, completed, or already discarded transaction branches can be discarded by their owner.");
        }
    }

    internal void MarkDiscardedByFork(ExecutionTransactionForkGroup ownerFork)
    {
        EnsureCanBeDiscardedByFork(ownerFork);
        Lifecycle = ExecutionTransactionLifecycle.Discarded;
    }

    private void EnsureStateAccessible()
    {
        EnsureActive();
        if (_openFork is not null)
        {
            throw new InvalidOperationException("Owner transaction state is frozen while a fork group is open; use the fork continuation branch instead.");
        }
    }

    private void EnsureActive()
    {
        if (Lifecycle != ExecutionTransactionLifecycle.Active)
        {
            throw new InvalidOperationException($"Transaction state is '{Lifecycle}' and cannot be used for this operation.");
        }
    }

    private void EnsureNoOpenFork()
    {
        if (_openFork is not null)
        {
            throw new InvalidOperationException("A transaction cannot complete or discard while it owns an open fork group.");
        }
    }
}
