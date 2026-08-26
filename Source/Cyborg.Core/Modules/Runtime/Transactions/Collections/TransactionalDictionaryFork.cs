using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions.Collections;

internal sealed class TransactionalDictionaryFork<TKey, TValue>
    where TKey : notnull
{
    private readonly TransactionalDictionarySnapshot<TKey, TValue> _baseline;
    private readonly TransactionalDictionary<TKey, TValue> _owner;

    public TransactionalDictionaryFork(TransactionalDictionary<TKey, TValue> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        _baseline = owner.Freeze();
    }

    public IReadOnlyDictionary<TKey, TValue> Baseline => _baseline;

    public TransactionalDictionary<TKey, TValue> CreateBranch()
    {
        EnsureOwnerUnchanged();
        return _owner.Fork();
    }

    public bool TrySelectChanges(
        ITransactionParticipant participant,
        IReadOnlyList<TransactionalDictionary<TKey, TValue>> contributors,
        Func<TKey, object> selectLogicalKey,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out Dictionary<TKey, TransactionalDictionaryChange<TValue>>? selectedChanges,
        out TransactionConflict? conflict)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(selectLogicalKey);
        ArgumentNullException.ThrowIfNull(conflictStrategy);
        EnsureOwnerUnchanged();

        Dictionary<TKey, List<(int ContributorIndex, TransactionalDictionaryChange<TValue> Change)>> changes =
            new(_baseline.Data.KeyComparer);
        for (int contributorIndex = 0; contributorIndex < contributors.Count; contributorIndex++)
        {
            TransactionalDictionary<TKey, TValue> contributor = contributors[contributorIndex];
            ArgumentNullException.ThrowIfNull(contributor);
            if (!ReferenceEquals(contributor.Baseline, _baseline))
            {
                throw new ArgumentException("Every merge contributor must derive from the fork baseline.", nameof(contributors));
            }

            foreach ((TKey key, TransactionalDictionaryChange<TValue> change) in contributor.EnumerateChanges())
            {
                if (!changes.TryGetValue(key, out List<(int ContributorIndex, TransactionalDictionaryChange<TValue> Change)>? keyChanges))
                {
                    keyChanges = [];
                    changes.Add(key, keyChanges);
                }
                keyChanges.Add((contributorIndex, change));
            }
        }

        selectedChanges = new Dictionary<TKey, TransactionalDictionaryChange<TValue>>(_baseline.Data.KeyComparer);
        foreach ((TKey key, List<(int ContributorIndex, TransactionalDictionaryChange<TValue> Change)> keyChanges) in changes)
        {
            if (keyChanges.Count == 1)
            {
                selectedChanges.Add(key, keyChanges[0].Change);
                continue;
            }

            ImmutableArray<int> contributorIndices = [.. keyChanges.Select(static change => change.ContributorIndex)];
            TransactionConflict detectedConflict = new(participant, selectLogicalKey(key), contributorIndices);
            TransactionConflictResolution resolution = conflictStrategy.Resolve(detectedConflict);
            switch (resolution.Kind)
            {
                case TransactionConflictResolutionKind.Fail:
                    selectedChanges = null;
                    conflict = detectedConflict;
                    return false;
                case TransactionConflictResolutionKind.UseContributor:
                    bool foundSelectedContributor = false;
                    foreach ((int contributorIndex, TransactionalDictionaryChange<TValue> change) in keyChanges)
                    {
                        if (contributorIndex != resolution.ContributorIndex)
                        {
                            continue;
                        }
                        selectedChanges.Add(key, change);
                        foundSelectedContributor = true;
                        break;
                    }
                    if (!foundSelectedContributor)
                    {
                        throw new InvalidOperationException("The conflict strategy selected a contributor that did not modify the conflicting dictionary state.");
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported transaction conflict resolution '{resolution.Kind}'.");
            }
        }

        conflict = null;
        return true;
    }

    public TransactionalDictionary<TKey, TValue> PrepareCandidate(
        IReadOnlyDictionary<TKey, TransactionalDictionaryChange<TValue>> selectedChanges)
    {
        ArgumentNullException.ThrowIfNull(selectedChanges);
        EnsureOwnerUnchanged();
        return _owner.PrepareMergeCandidate(_baseline, selectedChanges);
    }

    private void EnsureOwnerUnchanged()
    {
        if (!ReferenceEquals(_owner.Freeze(), _baseline))
        {
            throw new InvalidOperationException("The dictionary changed after the fork baseline was captured.");
        }
    }
}
