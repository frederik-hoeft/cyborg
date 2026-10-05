using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Environments.VirtualCollections;
using Cyborg.Core.Runtime.Engine.Transactions.Collections;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Transactions;

internal sealed class RuntimeEnvironmentBindingFork(TransactionalDictionary<EnvironmentVariableBinding, object?> values)
{
    private readonly TransactionalDictionaryFork<EnvironmentVariableBinding, object?> _values = new(values);

    public RuntimeEnvironmentBindingState CreateBranch() => new(_values.CreateBranch());

    public bool TryPrepareMerge(
        ITransactionParticipant participant,
        IReadOnlyList<RuntimeEnvironmentBindingState> contributors,
        IReadOnlySet<RuntimeEnvironmentId> retainedEnvironmentIds,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out RuntimeEnvironmentBindingState? candidate,
        [NotNullWhen(false)] out TransactionConflict? conflict)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(retainedEnvironmentIds);
        ArgumentNullException.ThrowIfNull(conflictStrategy);

        TransactionalDictionary<EnvironmentVariableBinding, object?>[] valueContributors = [.. contributors.Select(static state => state.Values)];
        ITransactionConflictStrategy bindingConflictStrategy = new RuntimeEnvironmentBindingConflictStrategy(conflictStrategy, valueContributors);
        if (!_values.TrySelectChanges(participant, valueContributors, static key => key, bindingConflictStrategy,
            out Dictionary<EnvironmentVariableBinding, TransactionalDictionaryChange<object?>>? valueChanges, out conflict))
        {
            candidate = null;
            return false;
        }

        Dictionary<EnvironmentVariableBinding, TransactionalDictionaryChange<object?>> retainedChanges = [];
        foreach ((EnvironmentVariableBinding binding, TransactionalDictionaryChange<object?> change) in valueChanges)
        {
            if (retainedEnvironmentIds.Contains(binding.EnvironmentId))
            {
                retainedChanges.Add(binding, change);
            }
        }

        candidate = new RuntimeEnvironmentBindingState(_values.PrepareCandidate(retainedChanges));
        conflict = null;
        return true;
    }

    private sealed class RuntimeEnvironmentBindingConflictStrategy(
        ITransactionConflictStrategy fallback,
        IReadOnlyList<TransactionalDictionary<EnvironmentVariableBinding, object?>> contributors) : ITransactionConflictStrategy
    {
        public TransactionConflictResolution Resolve(TransactionConflict conflict)
        {
            ArgumentNullException.ThrowIfNull(conflict);
            if (conflict.LogicalKey is EnvironmentVariableBinding binding && IsCompatibleVirtualCollectionChange(binding, conflict.ContributorIndices))
            {
                return TransactionConflictResolution.UseContributor(conflict.ContributorIndices[0]);
            }
            return fallback.Resolve(conflict);
        }

        private bool IsCompatibleVirtualCollectionChange(EnvironmentVariableBinding binding, ImmutableArray<int> contributorIndices)
        {
            if (!VirtualCollectionKeys.IsInternal(binding.Name))
            {
                return false;
            }

            TransactionalDictionaryChangeKind? commonKind = null;
            foreach (int contributorIndex in contributorIndices)
            {
                if (!contributors[contributorIndex].TryGetChange(binding, out TransactionalDictionaryChange<object?> change))
                {
                    return false;
                }
                commonKind ??= change.Kind;
                if (change.Kind != commonKind)
                {
                    return false;
                }
                if (change.Kind is TransactionalDictionaryChangeKind.Set
                    && (!VirtualCollectionKeys.IsMarker(binding.Name) || !VirtualCollectionElements.IsMergeCompatibleDefinitionMarker(change.Value)))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
