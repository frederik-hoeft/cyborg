using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class RuntimeEnvironmentTransactionState : ITransactionParticipantState
{
    private readonly RuntimeEnvironmentBindingState _bindings;
    private readonly RuntimeEnvironmentGraphState _graph;

    public RuntimeEnvironmentTransactionState(
        RuntimeEnvironmentId globalEnvironmentId,
        RuntimeEnvironmentGraphState graph,
        RuntimeEnvironmentBindingState bindings)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(bindings);
        GlobalEnvironmentId = globalEnvironmentId;
        _graph = graph;
        _bindings = bindings;
    }

    public RuntimeEnvironmentId GlobalEnvironmentId { get; }

    public bool ContainsEnvironment(RuntimeEnvironmentId environmentId) => _graph.ContainsEnvironment(environmentId);

    public bool TryGetEnvironment(RuntimeEnvironmentId environmentId, [NotNullWhen(true)] out RuntimeEnvironmentNode? node) =>
        _graph.TryGetEnvironment(environmentId, out node);

    public bool TryGetRegisteredEnvironment(string name, out RuntimeEnvironmentId environmentId) =>
        _graph.TryGetRegisteredEnvironment(name, out environmentId);

    public void AddEnvironment(
        RuntimeEnvironmentId environmentId,
        RuntimeEnvironmentNode node,
        IEnumerable<KeyValuePair<string, object?>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        KeyValuePair<string, object?>[] environmentValues = [.. values];
        _graph.AddEnvironment(environmentId, node);
        _bindings.SetValues(environmentId, environmentValues);
    }

    public bool TryAddNamedEnvironment(
        RuntimeEnvironmentId environmentId,
        RuntimeEnvironmentNode node,
        IEnumerable<KeyValuePair<string, object?>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        KeyValuePair<string, object?>[] environmentValues = [.. values];
        if (!_graph.TryAddNamedEnvironment(environmentId, node))
        {
            return false;
        }
        _bindings.SetValues(environmentId, environmentValues);
        return true;
    }

    public bool TryGetValue(RuntimeEnvironmentId environmentId, string name, out object? value) =>
        _bindings.TryGetValue(environmentId, name, out value);

    public void SetValue(RuntimeEnvironmentId environmentId, string name, object? value) =>
        _bindings.SetValue(environmentId, name, value);

    public bool TryRemove(RuntimeEnvironmentId environmentId, string name) =>
        _bindings.TryRemove(environmentId, name);

    public IEnumerable<KeyValuePair<string, object?>> EnumerateValues(RuntimeEnvironmentId environmentId) =>
        _bindings.EnumerateValues(environmentId);

    public ITransactionParticipantFork CreateFork() => new RuntimeEnvironmentTransactionFork(
        this,
        _graph.CreateFork(),
        _bindings.CreateFork());

    internal RuntimeEnvironmentGraphState Graph => _graph;

    internal RuntimeEnvironmentBindingState Bindings => _bindings;
}
