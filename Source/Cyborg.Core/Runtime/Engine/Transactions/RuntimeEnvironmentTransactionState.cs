using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine.Transactions;

internal sealed record RuntimeEnvironmentTransactionState
(
    RuntimeEnvironmentId GlobalEnvironmentId,
    RuntimeEnvironmentGraphState Graph,
    RuntimeEnvironmentBindingState Bindings
) : ITransactionParticipantState
{
    public bool ContainsEnvironment(RuntimeEnvironmentId environmentId) => Graph.ContainsEnvironment(environmentId);

    public bool TryGetEnvironment(RuntimeEnvironmentId environmentId, [NotNullWhen(true)] out RuntimeEnvironmentNode? node) =>
        Graph.TryGetEnvironment(environmentId, out node);

    public bool TryGetRegisteredEnvironment(string name, out RuntimeEnvironmentId environmentId) =>
        Graph.TryGetRegisteredEnvironment(name, out environmentId);

    public void AddEnvironment(RuntimeEnvironmentId environmentId, RuntimeEnvironmentNode node, IEnumerable<KeyValuePair<string, object?>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        KeyValuePair<string, object?>[] environmentValues = [.. values];
        Graph.AddEnvironment(environmentId, node);
        Bindings.SetValues(environmentId, environmentValues);
    }

    public bool TryAddNamedEnvironment(RuntimeEnvironmentId environmentId, RuntimeEnvironmentNode node, IEnumerable<KeyValuePair<string, object?>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        KeyValuePair<string, object?>[] environmentValues = [.. values];
        if (!Graph.TryAddNamedEnvironment(environmentId, node))
        {
            return false;
        }
        Bindings.SetValues(environmentId, environmentValues);
        return true;
    }

    public bool TryGetValue(RuntimeEnvironmentId environmentId, string name, out object? value) =>
        Bindings.TryGetValue(environmentId, name, out value);

    public void SetValue(RuntimeEnvironmentId environmentId, string name, object? value) =>
        Bindings.SetValue(environmentId, name, value);

    public bool TryRemove(RuntimeEnvironmentId environmentId, string name) =>
        Bindings.TryRemove(environmentId, name);

    public IEnumerable<KeyValuePair<string, object?>> EnumerateValues(RuntimeEnvironmentId environmentId) =>
        Bindings.EnumerateValues(environmentId);

    public ITransactionParticipantFork CreateFork() =>
        new RuntimeEnvironmentTransactionFork(this, Graph.CreateFork(), Bindings.CreateFork());
}
