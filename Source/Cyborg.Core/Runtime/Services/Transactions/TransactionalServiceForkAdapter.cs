namespace Cyborg.Core.Runtime.Services.Transactions;

internal sealed class TransactionalServiceForkAdapter<TState>(TransactionalServiceFork<TState> fork) : ITransactionalServiceForkAdapter
    where TState : class
{
    private readonly TransactionalServiceFork<TState> _fork = fork ?? throw new ArgumentNullException(nameof(fork));

    public object CreateBranch() =>
        _fork.CreateBranch() ?? throw new InvalidOperationException("A transactional service fork returned a null branch state.");

    public bool TryPrepareMerge(
        object ownerContinuation,
        IReadOnlyList<object> children,
        ITransactionalServiceConflictResolver conflictResolver,
        [NotNullWhen(true)] out object? candidate)
    {
        ArgumentNullException.ThrowIfNull(ownerContinuation);
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(conflictResolver);
        if (ownerContinuation is not TState typedOwnerContinuation)
        {
            throw new InvalidOperationException(
                $"Transactional service owner-continuation state type '{ownerContinuation.GetType().FullName}' does not match expected type '{typeof(TState).FullName}'.");
        }

        TState[] typedChildren = new TState[children.Count];
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not TState typedChild)
            {
                throw new InvalidOperationException(
                    $"Transactional service child state type '{children[i].GetType().FullName}' does not match expected type '{typeof(TState).FullName}'.");
            }
            typedChildren[i] = typedChild;
        }

        if (!_fork.TryPrepareMerge(typedOwnerContinuation, typedChildren, conflictResolver, out TState? typedCandidate))
        {
            candidate = null;
            return false;
        }
        candidate = typedCandidate
            ?? throw new InvalidOperationException("A transactional service fork returned success with a null candidate state.");
        return true;
    }
}
