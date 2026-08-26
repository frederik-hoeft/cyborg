using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions.Services;

internal sealed class TransactionalServiceForkAdapter<TState>(TransactionalServiceFork<TState> fork) : ITransactionalServiceForkAdapter
    where TState : class
{
    private readonly TransactionalServiceFork<TState> _fork = fork ?? throw new ArgumentNullException(nameof(fork));

    public object CreateBranch() =>
        _fork.CreateBranch() ?? throw new InvalidOperationException("A transactional service fork returned a null branch state.");

    public bool TryPrepareMerge(
        IReadOnlyList<object> contributors,
        ITransactionalServiceConflictResolver conflictResolver,
        [NotNullWhen(true)] out object? candidate)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(conflictResolver);
        TState[] typedContributors = new TState[contributors.Count];
        for (int i = 0; i < contributors.Count; i++)
        {
            if (contributors[i] is not TState typedContributor)
            {
                throw new InvalidOperationException(
                    $"Transactional service contributor state type '{contributors[i].GetType().FullName}' does not match expected type '{typeof(TState).FullName}'.");
            }
            typedContributors[i] = typedContributor;
        }

        if (!_fork.TryPrepareMerge(typedContributors, conflictResolver, out TState? typedCandidate))
        {
            candidate = null;
            return false;
        }
        candidate = typedCandidate
            ?? throw new InvalidOperationException("A transactional service fork returned success with a null candidate state.");
        return true;
    }
}
