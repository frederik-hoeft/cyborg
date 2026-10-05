namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal sealed class ModuleTransaction
(
    TransactionCoordinator coordinator,
    ModuleTransaction? parent,
    ModuleTransactionForkGroup? ownerFork,
    TransactionStateBundle state,
    string orderPrefix = ""
)
{
    private readonly ModuleTransactionForkGroup? _ownerFork = ownerFork;

    private ModuleTransactionForkGroup? _openFork;

    private TransactionStateBundle _state = state;

    private int _nextOrderSlot;

    public ModuleTransaction? Parent { get; } = parent;

    public bool IsRoot => Parent is null;

    /// <summary>
    /// Allocates the next order scope for a virtual-collection element or a nested fork.
    /// Scopes from one transaction sort in allocation order. A fork scope sorts between earlier and later allocations on this transaction.
    /// </summary>
    internal string AllocateCollectionOrderToken() => QualifyOrderScope(orderPrefix, _nextOrderSlot++);

    internal static string QualifyOrderScope(string prefix, int slot)
    {
        string segment = slot.ToString("D10");
        return prefix.Length == 0 ? segment : string.Concat(prefix, "/", segment);
    }

    public ModuleTransactionLifecycle Lifecycle { get; private set; } = ModuleTransactionLifecycle.Active;

    /// <summary>Completion disposition, interpreted separately by each participant's contribution policy.</summary>
    internal TransactionPublicationDisposition Publication { get; private set; } = TransactionPublicationDisposition.Commit;

    internal bool HasOpenFork => _openFork is not null;

    public TState GetParticipantState<TState>(ITransactionParticipant<TState> participant)
        where TState : ITransactionParticipantState
    {
        EnsureStateAccessible();
        return Volatile.Read(ref _state).Get(participant);
    }

    public ModuleTransactionForkGroup Fork()
    {
        EnsureActive();
        if (_openFork is not null)
        {
            throw new InvalidOperationException("A transaction cannot open another fork group before its current fork group closes.");
        }

        ModuleTransactionForkGroup fork = new(this, coordinator, Volatile.Read(ref _state));
        _openFork = fork;
        return fork;
    }

    public void Complete() => Complete(TransactionPublicationDisposition.Commit);

    internal void Complete(TransactionPublicationDisposition publication)
    {
        EnsureActive();
        EnsureNoOpenFork();
        if (!Enum.IsDefined(publication))
        {
            throw new ArgumentOutOfRangeException(nameof(publication), publication, "Unsupported transaction publication disposition.");
        }
        Publication = publication;
        Lifecycle = ModuleTransactionLifecycle.Completed;
    }

    public void Discard()
    {
        EnsureActive();
        EnsureNoOpenFork();
        Lifecycle = ModuleTransactionLifecycle.Discarded;
    }

    internal TransactionStateBundle GetStateForReconciliation(ModuleTransactionForkGroup ownerFork)
    {
        if (!ReferenceEquals(_ownerFork, ownerFork))
        {
            throw new InvalidOperationException("The transaction does not belong to the supplied fork group.");
        }
        if (Lifecycle != ModuleTransactionLifecycle.Completed)
        {
            throw new InvalidOperationException("Only completed transaction branches can participate in reconciliation.");
        }
        return Volatile.Read(ref _state);
    }

    internal void PublishForkResult(ModuleTransactionForkGroup fork, TransactionStateBundle state)
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

    internal void ReleaseForkWithoutPublication(ModuleTransactionForkGroup fork)
    {
        EnsureActive();
        if (!ReferenceEquals(_openFork, fork))
        {
            throw new InvalidOperationException("The supplied fork group is not active on this transaction.");
        }
        _openFork = null;
    }

    internal void MarkJoined(ModuleTransactionForkGroup ownerFork)
    {
        if (!ReferenceEquals(_ownerFork, ownerFork))
        {
            throw new InvalidOperationException("The transaction does not belong to the supplied fork group.");
        }
        if (Lifecycle != ModuleTransactionLifecycle.Completed)
        {
            throw new InvalidOperationException("Only completed transaction branches can be joined.");
        }
        Lifecycle = ModuleTransactionLifecycle.Joined;
    }

    internal void EnsureCanBeDiscardedByFork(ModuleTransactionForkGroup ownerFork)
    {
        if (!ReferenceEquals(_ownerFork, ownerFork))
        {
            throw new InvalidOperationException("The transaction does not belong to the supplied fork group.");
        }
        if (_openFork is not null)
        {
            throw new InvalidOperationException("A transaction branch with an open nested fork cannot be discarded by its owner.");
        }
        if (Lifecycle is not ModuleTransactionLifecycle.Active
            and not ModuleTransactionLifecycle.Completed
            and not ModuleTransactionLifecycle.Discarded)
        {
            throw new InvalidOperationException("Only active, completed, or already discarded transaction branches can be discarded by their owner.");
        }
    }

    internal void MarkDiscardedByFork(ModuleTransactionForkGroup ownerFork)
    {
        EnsureCanBeDiscardedByFork(ownerFork);
        Lifecycle = ModuleTransactionLifecycle.Discarded;
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
        if (Lifecycle != ModuleTransactionLifecycle.Active)
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
