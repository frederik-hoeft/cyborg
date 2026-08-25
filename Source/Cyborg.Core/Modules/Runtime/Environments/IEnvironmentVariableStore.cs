using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal interface IEnvironmentVariableStore : IEnumerable<KeyValuePair<string, object?>>
{
    RuntimeEnvironmentId Id { get; }

    bool TryGetValue(string name, out object? value);

    void SetValue(string name, object? value);

    bool TryRemove(string name);

    IEnvironmentVariableStore Bind(
        EnvironmentVariableTransactionParticipant participant,
        ExecutionTransaction transaction);

    EnvironmentVariableStoreSeed CaptureSeed();
}
