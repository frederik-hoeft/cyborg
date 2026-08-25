using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Collections;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal sealed class TransactionalEnvironmentVariableStore(
    RuntimeEnvironmentId environmentId,
    RuntimeEnvironmentTransactionParticipant participant,
    ExecutionTransaction transaction) : IEnvironmentVariableStore
{
    public bool TryGetValue(string name, out object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetState().TryGetValue(environmentId, name, out value);
    }

    public void SetValue(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        GetState().SetValue(environmentId, name, value);
    }

    public bool TryRemove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetState().TryRemove(environmentId, name);
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => GetState().EnumerateValues(environmentId).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private RuntimeEnvironmentTransactionState GetState() => transaction.GetParticipantState(participant);
}
