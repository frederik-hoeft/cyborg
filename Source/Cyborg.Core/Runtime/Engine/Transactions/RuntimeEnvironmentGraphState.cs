using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Collections;

namespace Cyborg.Core.Runtime.Engine.Transactions;

internal sealed record RuntimeEnvironmentGraphState
(
    TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> Nodes,
    TransactionalDictionary<string, RuntimeEnvironmentId> Registrations
)
{
    public bool ContainsEnvironment(RuntimeEnvironmentId environmentId) => Nodes.ContainsKey(environmentId);

    public bool TryGetEnvironment(RuntimeEnvironmentId environmentId, [NotNullWhen(true)] out RuntimeEnvironmentNode? node) =>
        Nodes.TryGetValue(environmentId, out node);

    public bool TryGetRegisteredEnvironment(string name, out RuntimeEnvironmentId environmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Registrations.TryGetValue(name, out environmentId);
    }

    public void AddEnvironment(RuntimeEnvironmentId environmentId, RuntimeEnvironmentNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!Nodes.TryAdd(environmentId, node))
        {
            throw new InvalidOperationException($"Runtime environment '{environmentId}' already exists in the current transaction.");
        }
    }

    public bool TryAddNamedEnvironment(RuntimeEnvironmentId environmentId, RuntimeEnvironmentNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.IsTransient)
        {
            throw new ArgumentException("A transient runtime environment cannot be added to the named environment catalog.", nameof(node));
        }
        if (Registrations.ContainsKey(node.Name))
        {
            return false;
        }

        AddEnvironment(environmentId, node);
        if (!Registrations.TryAdd(node.Name, environmentId))
        {
            throw new InvalidOperationException("Named runtime environment registration changed unexpectedly while adding environment topology.");
        }
        return true;
    }

    public RuntimeEnvironmentGraphFork CreateFork() => new(Nodes, Registrations);
}
