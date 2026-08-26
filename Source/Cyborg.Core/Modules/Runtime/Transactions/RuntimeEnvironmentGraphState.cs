using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class RuntimeEnvironmentGraphState
{
    private readonly TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> _nodes;
    private readonly TransactionalDictionary<string, RuntimeEnvironmentId> _registrations;

    public RuntimeEnvironmentGraphState(
        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> nodes,
        TransactionalDictionary<string, RuntimeEnvironmentId> registrations)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(registrations);
        _nodes = nodes;
        _registrations = registrations;
    }

    public bool ContainsEnvironment(RuntimeEnvironmentId environmentId) => _nodes.ContainsKey(environmentId);

    public bool TryGetEnvironment(RuntimeEnvironmentId environmentId, [NotNullWhen(true)] out RuntimeEnvironmentNode? node) =>
        _nodes.TryGetValue(environmentId, out node);

    public bool TryGetRegisteredEnvironment(string name, out RuntimeEnvironmentId environmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _registrations.TryGetValue(name, out environmentId);
    }

    public void AddEnvironment(RuntimeEnvironmentId environmentId, RuntimeEnvironmentNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_nodes.TryAdd(environmentId, node))
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
        if (_registrations.ContainsKey(node.Name))
        {
            return false;
        }

        AddEnvironment(environmentId, node);
        if (!_registrations.TryAdd(node.Name, environmentId))
        {
            throw new InvalidOperationException("Named runtime environment registration changed unexpectedly while adding environment topology.");
        }
        return true;
    }

    public RuntimeEnvironmentGraphFork CreateFork() => new(_nodes, _registrations);

    internal TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> Nodes => _nodes;

    internal TransactionalDictionary<string, RuntimeEnvironmentId> Registrations => _registrations;
}
