using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Collections;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class RuntimeEnvironmentTransactionParticipant : ITransactionParticipant<RuntimeEnvironmentTransactionState>
{
    public RuntimeEnvironmentTransactionState CreateRootState(TransactionRootSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (!seed.TryGet(this, out RuntimeEnvironmentTransactionSeed environmentSeed))
        {
            throw new InvalidOperationException("The runtime environment transaction participant requires an environment root seed.");
        }

        List<KeyValuePair<RuntimeEnvironmentId, RuntimeEnvironmentNode>> nodes = [];
        List<KeyValuePair<string, RuntimeEnvironmentId>> registrations = [];
        List<KeyValuePair<EnvironmentVariableBinding, object?>> values = [];
        foreach (RuntimeEnvironmentSeed environment in environmentSeed.Environments)
        {
            nodes.Add(new KeyValuePair<RuntimeEnvironmentId, RuntimeEnvironmentNode>(environment.EnvironmentId, environment.Node));
            if (environment.RegisterName)
            {
                registrations.Add(new KeyValuePair<string, RuntimeEnvironmentId>(environment.Node.Name, environment.EnvironmentId));
            }
            foreach ((string name, object? value) in environment.Values)
            {
                values.Add(new KeyValuePair<EnvironmentVariableBinding, object?>(
                    new EnvironmentVariableBinding(environment.EnvironmentId, name),
                    value));
            }
        }

        if (!nodes.Any(node => node.Key == environmentSeed.GlobalEnvironmentId))
        {
            throw new InvalidOperationException("The environment root seed does not contain its logical global environment.");
        }

        return new RuntimeEnvironmentTransactionState(
            environmentSeed.GlobalEnvironmentId,
            new TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode>(nodes),
            new TransactionalDictionary<string, RuntimeEnvironmentId>(registrations, StringComparer.Ordinal),
            new TransactionalDictionary<EnvironmentVariableBinding, object?>(values));
    }
}

internal sealed class RuntimeEnvironmentTransactionState : ITransactionParticipantState
{
    private readonly TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> _nodes;
    private readonly TransactionalDictionary<string, RuntimeEnvironmentId> _registrations;
    private readonly TransactionalDictionary<EnvironmentVariableBinding, object?> _values;

    public RuntimeEnvironmentTransactionState(
        RuntimeEnvironmentId globalEnvironmentId,
        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> nodes,
        TransactionalDictionary<string, RuntimeEnvironmentId> registrations,
        TransactionalDictionary<EnvironmentVariableBinding, object?> values)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(values);
        GlobalEnvironmentId = globalEnvironmentId;
        _nodes = nodes;
        _registrations = registrations;
        _values = values;
    }

    public RuntimeEnvironmentId GlobalEnvironmentId { get; }

    public bool ContainsEnvironment(RuntimeEnvironmentId environmentId) => _nodes.ContainsKey(environmentId);

    public bool TryGetEnvironment(RuntimeEnvironmentId environmentId, [NotNullWhen(true)] out RuntimeEnvironmentNode? node) =>
        _nodes.TryGetValue(environmentId, out node);

    public bool TryGetRegisteredEnvironment(string name, out RuntimeEnvironmentId environmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _registrations.TryGetValue(name, out environmentId);
    }

    public void AddEnvironment(
        RuntimeEnvironmentId environmentId,
        RuntimeEnvironmentNode node,
        IEnumerable<KeyValuePair<string, object?>> values)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(values);
        if (!_nodes.TryAdd(environmentId, node))
        {
            throw new InvalidOperationException($"Runtime environment '{environmentId}' already exists in the current transaction.");
        }
        foreach ((string name, object? value) in values)
        {
            _values.Set(new EnvironmentVariableBinding(environmentId, name), value);
        }
    }

    public bool TryAddNamedEnvironment(
        RuntimeEnvironmentId environmentId,
        RuntimeEnvironmentNode node,
        IEnumerable<KeyValuePair<string, object?>> values)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(values);
        if (node.IsTransient)
        {
            throw new ArgumentException("A transient runtime environment cannot be added to the named environment catalog.", nameof(node));
        }
        if (_registrations.ContainsKey(node.Name))
        {
            return false;
        }
        AddEnvironment(environmentId, node, values);
        if (!_registrations.TryAdd(node.Name, environmentId))
        {
            throw new InvalidOperationException("Named runtime environment registration changed unexpectedly while adding environment topology.");
        }
        return true;
    }

    public bool TryGetValue(RuntimeEnvironmentId environmentId, string name, out object? value) =>
        _values.TryGetValue(new EnvironmentVariableBinding(environmentId, name), out value);

    public void SetValue(RuntimeEnvironmentId environmentId, string name, object? value) =>
        _values.Set(new EnvironmentVariableBinding(environmentId, name), value);

    public bool TryRemove(RuntimeEnvironmentId environmentId, string name) =>
        _values.TryRemove(new EnvironmentVariableBinding(environmentId, name));

    public IEnumerable<KeyValuePair<string, object?>> EnumerateValues(RuntimeEnvironmentId environmentId)
    {
        foreach ((EnvironmentVariableBinding binding, object? value) in _values)
        {
            if (binding.EnvironmentId == environmentId)
            {
                yield return new KeyValuePair<string, object?>(binding.Name, value);
            }
        }
    }

    public ITransactionParticipantFork CreateFork() => new RuntimeEnvironmentTransactionFork(
        this,
        _nodes.Freeze(),
        _registrations.Freeze(),
        _values.Freeze());

    internal TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> Nodes => _nodes;

    internal TransactionalDictionary<string, RuntimeEnvironmentId> Registrations => _registrations;

    internal TransactionalDictionary<EnvironmentVariableBinding, object?> Values => _values;
}

internal sealed class RuntimeEnvironmentTransactionFork : ITransactionParticipantFork
{
    private readonly TransactionalDictionarySnapshot<RuntimeEnvironmentId, RuntimeEnvironmentNode> _nodeBaseline;
    private readonly RuntimeEnvironmentTransactionState _owner;
    private readonly TransactionalDictionarySnapshot<string, RuntimeEnvironmentId> _registrationBaseline;
    private readonly TransactionalDictionarySnapshot<EnvironmentVariableBinding, object?> _valueBaseline;

    public RuntimeEnvironmentTransactionFork(
        RuntimeEnvironmentTransactionState owner,
        TransactionalDictionarySnapshot<RuntimeEnvironmentId, RuntimeEnvironmentNode> nodeBaseline,
        TransactionalDictionarySnapshot<string, RuntimeEnvironmentId> registrationBaseline,
        TransactionalDictionarySnapshot<EnvironmentVariableBinding, object?> valueBaseline)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(nodeBaseline);
        ArgumentNullException.ThrowIfNull(registrationBaseline);
        ArgumentNullException.ThrowIfNull(valueBaseline);
        _owner = owner;
        _nodeBaseline = nodeBaseline;
        _registrationBaseline = registrationBaseline;
        _valueBaseline = valueBaseline;
    }

    public ITransactionParticipantState CreateBranch() =>
        new RuntimeEnvironmentTransactionState(
            _owner.GlobalEnvironmentId,
            _owner.Nodes.Fork(),
            _owner.Registrations.Fork(),
            _owner.Values.Fork());

    public bool TryPrepareMerge(
        ITransactionParticipant participant,
        IReadOnlyList<ITransactionParticipantState> contributors,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out ITransactionParticipantState? candidate,
        out TransactionConflict? conflict)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(conflictStrategy);

        RuntimeEnvironmentTransactionState[] environmentContributors = new RuntimeEnvironmentTransactionState[contributors.Count];
        for (int i = 0; i < contributors.Count; i++)
        {
            environmentContributors[i] = (RuntimeEnvironmentTransactionState)contributors[i];
        }

        if (!TrySelectChanges(
            participant,
            environmentContributors,
            static state => state.Nodes,
            static key => new RuntimeEnvironmentTopologyConflictKey(key),
            conflictStrategy,
            out Dictionary<RuntimeEnvironmentId, TransactionalDictionaryChange<RuntimeEnvironmentNode>>? nodeChanges,
            out conflict)
            || !TrySelectChanges(
                participant,
                environmentContributors,
                static state => state.Registrations,
                static key => new RuntimeEnvironmentRegistrationConflictKey(key),
                conflictStrategy,
                out Dictionary<string, TransactionalDictionaryChange<RuntimeEnvironmentId>>? registrationChanges,
                out conflict)
            || !TrySelectChanges(
                participant,
                environmentContributors,
                static state => state.Values,
                static key => key,
                conflictStrategy,
                out Dictionary<EnvironmentVariableBinding, TransactionalDictionaryChange<object?>>? valueChanges,
                out conflict))
        {
            candidate = null;
            return false;
        }

        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> preliminaryNodes =
            _owner.Nodes.PrepareMergeCandidate(_nodeBaseline, nodeChanges);
        TransactionalDictionary<string, RuntimeEnvironmentId> preliminaryRegistrations =
            _owner.Registrations.PrepareMergeCandidate(_registrationBaseline, registrationChanges);
        HashSet<RuntimeEnvironmentId> retainedEnvironmentIds = DetermineRetainedEnvironmentIds(preliminaryNodes, preliminaryRegistrations);

        Dictionary<RuntimeEnvironmentId, TransactionalDictionaryChange<RuntimeEnvironmentNode>> retainedNodeChanges = [];
        foreach ((RuntimeEnvironmentId environmentId, TransactionalDictionaryChange<RuntimeEnvironmentNode> change) in nodeChanges)
        {
            if (change.Kind == TransactionalDictionaryChangeKind.Remove
                || retainedEnvironmentIds.Contains(environmentId))
            {
                retainedNodeChanges.Add(environmentId, change);
            }
        }

        Dictionary<EnvironmentVariableBinding, TransactionalDictionaryChange<object?>> retainedValueChanges = [];
        foreach ((EnvironmentVariableBinding binding, TransactionalDictionaryChange<object?> change) in valueChanges)
        {
            if (retainedEnvironmentIds.Contains(binding.EnvironmentId))
            {
                retainedValueChanges.Add(binding, change);
            }
        }

        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> mergedNodes =
            _owner.Nodes.PrepareMergeCandidate(_nodeBaseline, retainedNodeChanges);
        TransactionalDictionary<string, RuntimeEnvironmentId> mergedRegistrations =
            _owner.Registrations.PrepareMergeCandidate(_registrationBaseline, registrationChanges);
        ValidateRegistrations(mergedNodes, mergedRegistrations);
        TransactionalDictionary<EnvironmentVariableBinding, object?> mergedValues =
            _owner.Values.PrepareMergeCandidate(_valueBaseline, retainedValueChanges);

        candidate = new RuntimeEnvironmentTransactionState(
            _owner.GlobalEnvironmentId,
            mergedNodes,
            mergedRegistrations,
            mergedValues);
        conflict = null;
        return true;
    }

    private HashSet<RuntimeEnvironmentId> DetermineRetainedEnvironmentIds(
        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> nodes,
        TransactionalDictionary<string, RuntimeEnvironmentId> registrations)
    {
        HashSet<RuntimeEnvironmentId> retained = [.. _nodeBaseline.Keys];
        foreach (RuntimeEnvironmentId environmentId in registrations.Values)
        {
            AddEnvironmentAndAncestors(environmentId, nodes, retained);
        }
        return retained;
    }

    private static void AddEnvironmentAndAncestors(
        RuntimeEnvironmentId environmentId,
        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> nodes,
        HashSet<RuntimeEnvironmentId> retained)
    {
        RuntimeEnvironmentId current = environmentId;
        while (retained.Add(current))
        {
            if (!nodes.TryGetValue(current, out RuntimeEnvironmentNode? node))
            {
                throw new InvalidOperationException("A named runtime environment references topology that does not exist in the candidate transaction state.");
            }
            if (node.Parent is not RuntimeEnvironmentParent parent)
            {
                break;
            }
            current = parent.EnvironmentId;
        }
    }

    private static void ValidateRegistrations(
        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> nodes,
        TransactionalDictionary<string, RuntimeEnvironmentId> registrations)
    {
        foreach ((string name, RuntimeEnvironmentId environmentId) in registrations)
        {
            if (!nodes.TryGetValue(environmentId, out RuntimeEnvironmentNode? node))
            {
                throw new InvalidOperationException($"Named runtime environment '{name}' references topology that does not exist.");
            }
            if (!node.Name.Equals(name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Named runtime environment registration '{name}' does not match environment node name '{node.Name}'.");
            }
            if (node.IsTransient)
            {
                throw new InvalidOperationException($"Transient runtime environment '{name}' cannot be retained as a named registration.");
            }
        }
    }

    private static bool TrySelectChanges<TKey, TValue>(
        ITransactionParticipant participant,
        IReadOnlyList<RuntimeEnvironmentTransactionState> contributors,
        Func<RuntimeEnvironmentTransactionState, TransactionalDictionary<TKey, TValue>> selectDictionary,
        Func<TKey, object> selectLogicalKey,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out Dictionary<TKey, TransactionalDictionaryChange<TValue>>? selectedChanges,
        out TransactionConflict? conflict)
        where TKey : notnull
    {
        Dictionary<TKey, List<(int ContributorIndex, TransactionalDictionaryChange<TValue> Change)>> changes = [];
        for (int contributorIndex = 0; contributorIndex < contributors.Count; contributorIndex++)
        {
            TransactionalDictionary<TKey, TValue> dictionary = selectDictionary(contributors[contributorIndex]);
            foreach ((TKey key, TransactionalDictionaryChange<TValue> change) in dictionary.EnumerateChanges())
            {
                if (!changes.TryGetValue(key, out List<(int ContributorIndex, TransactionalDictionaryChange<TValue> Change)>? keyChanges))
                {
                    keyChanges = [];
                    changes.Add(key, keyChanges);
                }
                keyChanges.Add((contributorIndex, change));
            }
        }

        selectedChanges = [];
        foreach ((TKey key, List<(int ContributorIndex, TransactionalDictionaryChange<TValue> Change)> keyChanges) in changes)
        {
            if (keyChanges.Count == 1)
            {
                selectedChanges.Add(key, keyChanges[0].Change);
                continue;
            }

            ImmutableArray<int> contributorIndices = [.. keyChanges.Select(static change => change.ContributorIndex)];
            TransactionConflict detectedConflict = new(participant, selectLogicalKey(key), contributorIndices);
            TransactionConflictResolution resolution = conflictStrategy.Resolve(detectedConflict);
            switch (resolution.Kind)
            {
                case TransactionConflictResolutionKind.Fail:
                    selectedChanges = null;
                    conflict = detectedConflict;
                    return false;
                case TransactionConflictResolutionKind.UseContributor:
                    bool foundSelectedContributor = false;
                    foreach ((int contributorIndex, TransactionalDictionaryChange<TValue> change) in keyChanges)
                    {
                        if (contributorIndex != resolution.ContributorIndex)
                        {
                            continue;
                        }
                        selectedChanges.Add(key, change);
                        foundSelectedContributor = true;
                        break;
                    }
                    if (!foundSelectedContributor)
                    {
                        throw new InvalidOperationException("The conflict strategy selected a contributor that did not modify the conflicting runtime environment state.");
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported transaction conflict resolution '{resolution.Kind}'.");
            }
        }

        conflict = null;
        return true;
    }

    private readonly record struct RuntimeEnvironmentRegistrationConflictKey(string Name);

    private readonly record struct RuntimeEnvironmentTopologyConflictKey(RuntimeEnvironmentId EnvironmentId);
}
