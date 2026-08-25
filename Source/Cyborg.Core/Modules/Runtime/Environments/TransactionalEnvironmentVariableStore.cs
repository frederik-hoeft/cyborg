using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Collections;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal sealed class TransactionalEnvironmentVariableStore(
    RuntimeEnvironmentId id,
    EnvironmentVariableTransactionParticipant participant,
    ExecutionTransaction transaction) : IEnvironmentVariableStore
{
    public RuntimeEnvironmentId Id => id;

    public bool TryGetValue(string name, out object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetState().TryGetValue(id, name, out value);
    }

    public void SetValue(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        GetState().SetValue(id, name, value);
    }

    public bool TryRemove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetState().TryRemove(id, name);
    }

    public IEnvironmentVariableStore Bind(
        EnvironmentVariableTransactionParticipant transactionParticipant,
        ExecutionTransaction executionTransaction)
    {
        ArgumentNullException.ThrowIfNull(transactionParticipant);
        ArgumentNullException.ThrowIfNull(executionTransaction);
        if (!ReferenceEquals(participant, transactionParticipant))
        {
            throw new InvalidOperationException("An environment variable store cannot be rebound to a different transaction participant descriptor.");
        }
        return ReferenceEquals(transaction, executionTransaction)
            ? this
            : new TransactionalEnvironmentVariableStore(id, participant, executionTransaction);
    }

    public EnvironmentVariableStoreSeed CaptureSeed() =>
        new(id, [.. this]);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => GetState().Enumerate(id).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private EnvironmentVariableTransactionState GetState() => transaction.GetParticipantState(participant);
}
