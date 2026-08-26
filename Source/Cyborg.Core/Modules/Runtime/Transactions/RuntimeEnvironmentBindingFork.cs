using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Collections;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class RuntimeEnvironmentBindingFork
{
    private readonly TransactionalDictionaryFork<EnvironmentVariableBinding, object?> _values;

    public RuntimeEnvironmentBindingFork(TransactionalDictionary<EnvironmentVariableBinding, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new TransactionalDictionaryFork<EnvironmentVariableBinding, object?>(values);
    }

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

        TransactionalDictionary<EnvironmentVariableBinding, object?>[] valueContributors =
            [.. contributors.Select(static state => state.Values)];
        if (!_values.TrySelectChanges(
            participant,
            valueContributors,
            static key => key,
            conflictStrategy,
            out Dictionary<EnvironmentVariableBinding, TransactionalDictionaryChange<object?>>? valueChanges,
            out conflict))
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
