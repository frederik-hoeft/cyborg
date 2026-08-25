using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Collections;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal sealed class MutableEnvironmentVariableStore : IEnvironmentVariableStore
{
    private readonly Dictionary<string, object?> _variables = [];

    public RuntimeEnvironmentId Id { get; } = RuntimeEnvironmentId.Create();

    public bool TryGetValue(string name, out object? value) => _variables.TryGetValue(name, out value);

    public void SetValue(string name, object? value) => _variables[name] = value;

    public bool TryRemove(string name) => _variables.Remove(name);

    public IEnvironmentVariableStore Bind(
        EnvironmentVariableTransactionParticipant participant,
        ExecutionTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(transaction);
        return new TransactionalEnvironmentVariableStore(Id, participant, transaction);
    }

    public EnvironmentVariableStoreSeed CaptureSeed() => new(Id, [.. _variables]);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _variables.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
