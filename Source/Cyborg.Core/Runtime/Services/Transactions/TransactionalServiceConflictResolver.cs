using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.Transactions;

internal sealed class TransactionalServiceConflictResolver(
    ITransactionParticipant participant,
    ITransactionConflictStrategy strategy,
    int contributorCount) : ITransactionalServiceConflictResolver
{
    private readonly ITransactionParticipant _participant = participant ?? throw new ArgumentNullException(nameof(participant));
    private readonly ITransactionConflictStrategy _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    private readonly int _contributorCount = contributorCount > 0 ? contributorCount : throw new ArgumentOutOfRangeException(nameof(contributorCount));

    public TransactionConflict? UnresolvedConflict { get; private set; }

    public bool TryResolve(
        object logicalKey,
        IReadOnlyList<int> contributorIndices,
        out int selectedContributorIndex)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentNullException.ThrowIfNull(contributorIndices);
        if (contributorIndices.Count < 2)
        {
            throw new ArgumentException("A transaction conflict requires at least two contributors.", nameof(contributorIndices));
        }

        ImmutableArray<int> indices = [.. contributorIndices];
        HashSet<int> uniqueIndices = [];
        foreach (int contributorIndex in indices)
        {
            if (contributorIndex < 0 || contributorIndex >= _contributorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(contributorIndices), contributorIndex, "Conflict contributor index is outside the current merge contributor list.");
            }
            if (!uniqueIndices.Add(contributorIndex))
            {
                throw new ArgumentException("A transaction conflict cannot contain the same contributor more than once.", nameof(contributorIndices));
            }
        }
        TransactionConflict conflict = new(_participant, logicalKey, indices);
        TransactionConflictResolution resolution = _strategy.Resolve(conflict);
        if (resolution.Kind == TransactionConflictResolutionKind.Fail)
        {
            UnresolvedConflict = conflict;
            selectedContributorIndex = -1;
            return false;
        }
        if (!indices.Contains(resolution.ContributorIndex))
        {
            throw new InvalidOperationException(
                $"Transaction conflict strategy selected contributor '{resolution.ContributorIndex}', which is not part of the reported conflict.");
        }
        selectedContributorIndex = resolution.ContributorIndex;
        return true;
    }
}
