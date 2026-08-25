namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal sealed class ExecutionTransaction
{
    private readonly TransactionCoordinator _coordinator;
    private readonly ExecutionTransactionForkGroup? _ownerFork;
    private ExecutionTransactionForkGroup? _openFork;
    private TransactionStateBundle _state;
    private ExecutionTransactionLifecycle _lifecycle;

    internal ExecutionTransaction(
        TransactionCoordinator coordinator,
        ExecutionTransaction? parent,
        ExecutionTransactionForkGroup? ownerFork,
        TransactionStateBundle state)
    {
        _coordinator = coordinator;
        Parent = parent;
        _ownerFork = ownerFork;
        _state = state;
        _lifecycle = ExecutionTransactionLifecycle.Active;
    }

    public ExecutionTransaction? Parent { get; }

    public bool IsRoot => Parent is null;

    public ExecutionTransactionLifecycle Lifecycle => _lifecycle;

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

        ExecutionTransactionForkGroup fork = new(this, _coordinator, Volatile.Read(ref _state));
        _openFork = fork;
        return fork;
    }

    public void Complete()
    {
        EnsureActive();
        EnsureNoOpenFork();
        _lifecycle = ExecutionTransactionLifecycle.Completed;
    }

    public void Discard()
    {
        EnsureActive();
        EnsureNoOpenFork();
        _lifecycle = ExecutionTransactionLifecycle.Discarded;
    }

    internal TransactionStateBundle GetStateForReconciliation(ExecutionTransactionForkGroup ownerFork)
    {
        if (!ReferenceEquals(_ownerFork, ownerFork))
        {
            throw new InvalidOperationException("The transaction does not belong to the supplied fork group.");
        }
        if (_lifecycle != ExecutionTransactionLifecycle.Completed)
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
        if (_lifecycle != ExecutionTransactionLifecycle.Completed)
        {
            throw new InvalidOperationException("Only completed transaction branches can be joined.");
        }
        _lifecycle = ExecutionTransactionLifecycle.Joined;
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
        if (_lifecycle is not ExecutionTransactionLifecycle.Active
            and not ExecutionTransactionLifecycle.Completed
            and not ExecutionTransactionLifecycle.Discarded)
        {
            throw new InvalidOperationException("Only active, completed, or already discarded transaction branches can be discarded by their owner.");
        }
    }

    internal void MarkDiscardedByFork(ExecutionTransactionForkGroup ownerFork)
    {
        EnsureCanBeDiscardedByFork(ownerFork);
        _lifecycle = ExecutionTransactionLifecycle.Discarded;
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
        if (_lifecycle != ExecutionTransactionLifecycle.Active)
        {
            throw new InvalidOperationException($"Transaction state is '{_lifecycle}' and cannot be used for this operation.");
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
