using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Collections;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;

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
        out TransactionConflict? conflict)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(retainedEnvironmentIds);
        ArgumentNullException.ThrowIfNull(conflictStrategy);

        TransactionalDictionary<EnvironmentVariableBinding, object?>[] valueContributors = [.. contributors.Select(static state => state.Values)];
        if (!_values.TrySelectChanges(participant, valueContributors, static key => key, conflictStrategy, out Dictionary<EnvironmentVariableBinding, TransactionalDictionaryChange<object?>>? valueChanges, out conflict))
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
}
