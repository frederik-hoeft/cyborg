using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Collections;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class EnvironmentVariableTransactionParticipant : ITransactionParticipant<EnvironmentVariableTransactionState>
{
    public EnvironmentVariableTransactionState CreateRootState(TransactionRootSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        IEnumerable<KeyValuePair<EnvironmentVariableBinding, object?>> values = [];
        if (seed.TryGet<EnvironmentVariableStoreSeed[]>(this, out EnvironmentVariableStoreSeed[] seededStores))
        {
            values = seededStores.SelectMany(static store => store.Values.Select(value =>
                new KeyValuePair<EnvironmentVariableBinding, object?>(
                    new EnvironmentVariableBinding(store.EnvironmentId, value.Key),
                    value.Value)));
        }
        return new EnvironmentVariableTransactionState(new TransactionalDictionary<EnvironmentVariableBinding, object?>(values));
    }
}

internal sealed class EnvironmentVariableTransactionState : ITransactionParticipantState
{
    private readonly TransactionalDictionary<EnvironmentVariableBinding, object?> _values;

    public EnvironmentVariableTransactionState(TransactionalDictionary<EnvironmentVariableBinding, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values;
    }

    public bool TryGetValue(RuntimeEnvironmentId environmentId, string name, out object? value) =>
        _values.TryGetValue(new EnvironmentVariableBinding(environmentId, name), out value);

    public void SetValue(RuntimeEnvironmentId environmentId, string name, object? value) =>
        _values.Set(new EnvironmentVariableBinding(environmentId, name), value);

    public bool TryRemove(RuntimeEnvironmentId environmentId, string name) =>
        _values.TryRemove(new EnvironmentVariableBinding(environmentId, name));

    public IEnumerable<KeyValuePair<string, object?>> Enumerate(RuntimeEnvironmentId environmentId)
    {
        foreach ((EnvironmentVariableBinding binding, object? value) in _values)
        {
            if (binding.EnvironmentId == environmentId)
            {
                yield return new KeyValuePair<string, object?>(binding.Name, value);
            }
        }
    }

    public ITransactionParticipantFork CreateFork() => new EnvironmentVariableTransactionFork(this, _values.Freeze());

    internal TransactionalDictionary<EnvironmentVariableBinding, object?> Values => _values;
}

internal sealed class EnvironmentVariableTransactionFork : ITransactionParticipantFork
{
    private readonly TransactionalDictionarySnapshot<EnvironmentVariableBinding, object?> _baseline;
    private readonly EnvironmentVariableTransactionState _owner;

    public EnvironmentVariableTransactionFork(
        EnvironmentVariableTransactionState owner,
        TransactionalDictionarySnapshot<EnvironmentVariableBinding, object?> baseline)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(baseline);
        _owner = owner;
        _baseline = baseline;
    }

    public ITransactionParticipantState CreateBranch() =>
        new EnvironmentVariableTransactionState(_owner.Values.Fork());

    public bool TryPrepareMerge(
        ITransactionParticipant participant,
        IReadOnlyList<ITransactionParticipantState> contributors,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out ITransactionParticipantState? candidate,
        out TransactionConflict? conflict)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(conflictStrategy);

        Dictionary<EnvironmentVariableBinding, List<(int ContributorIndex, TransactionalDictionaryChange<object?> Change)>> changes = [];
        for (int contributorIndex = 0; contributorIndex < contributors.Count; contributorIndex++)
        {
            EnvironmentVariableTransactionState contributor = (EnvironmentVariableTransactionState)contributors[contributorIndex];
            foreach ((EnvironmentVariableBinding key, TransactionalDictionaryChange<object?> change) in contributor.Values.EnumerateChanges())
            {
                if (!changes.TryGetValue(key, out List<(int ContributorIndex, TransactionalDictionaryChange<object?> Change)>? keyChanges))
                {
                    keyChanges = [];
                    changes.Add(key, keyChanges);
                }
                keyChanges.Add((contributorIndex, change));
            }
        }

        Dictionary<EnvironmentVariableBinding, TransactionalDictionaryChange<object?>> selectedChanges = [];
        foreach ((EnvironmentVariableBinding key, List<(int ContributorIndex, TransactionalDictionaryChange<object?> Change)> keyChanges) in changes)
        {
            if (keyChanges.Count == 1)
            {
                selectedChanges.Add(key, keyChanges[0].Change);
                continue;
            }

            ImmutableArray<int> contributorIndices = [.. keyChanges.Select(static change => change.ContributorIndex)];
            TransactionConflict detectedConflict = new(participant, key, contributorIndices);
            TransactionConflictResolution resolution = conflictStrategy.Resolve(detectedConflict);
            switch (resolution.Kind)
            {
                case TransactionConflictResolutionKind.Fail:
                    candidate = null;
                    conflict = detectedConflict;
                    return false;
                case TransactionConflictResolutionKind.UseContributor:
                    bool foundSelectedContributor = false;
                    foreach ((int contributorIndex, TransactionalDictionaryChange<object?> change) in keyChanges)
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
                        throw new InvalidOperationException("The conflict strategy selected a contributor that did not modify the conflicting environment binding.");
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported transaction conflict resolution '{resolution.Kind}'.");
            }
        }

        candidate = new EnvironmentVariableTransactionState(_owner.Values.PrepareMergeCandidate(_baseline, selectedChanges));
        conflict = null;
        return true;
    }
}
