using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Collections;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class RuntimeEnvironmentGraphFork
{
    private readonly TransactionalDictionaryFork<RuntimeEnvironmentId, RuntimeEnvironmentNode> _nodes;
    private readonly TransactionalDictionaryFork<string, RuntimeEnvironmentId> _registrations;

    public RuntimeEnvironmentGraphFork(
        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> nodes,
        TransactionalDictionary<string, RuntimeEnvironmentId> registrations)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(registrations);
        _nodes = new TransactionalDictionaryFork<RuntimeEnvironmentId, RuntimeEnvironmentNode>(nodes);
        _registrations = new TransactionalDictionaryFork<string, RuntimeEnvironmentId>(registrations);
    }

    public RuntimeEnvironmentGraphState CreateBranch() => new(
        _nodes.CreateBranch(),
        _registrations.CreateBranch());

    public bool TryPrepareMerge(
        ITransactionParticipant participant,
        IReadOnlyList<RuntimeEnvironmentGraphState> contributors,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out RuntimeEnvironmentGraphState? candidate,
        [NotNullWhen(true)] out HashSet<RuntimeEnvironmentId>? retainedEnvironmentIds,
        out TransactionConflict? conflict)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(conflictStrategy);

        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode>[] nodeContributors =
            [.. contributors.Select(static state => state.Nodes)];
        TransactionalDictionary<string, RuntimeEnvironmentId>[] registrationContributors =
            [.. contributors.Select(static state => state.Registrations)];

        if (!_nodes.TrySelectChanges(
                participant,
                nodeContributors,
                static key => new RuntimeEnvironmentTopologyConflictKey(key),
                conflictStrategy,
                out Dictionary<RuntimeEnvironmentId, TransactionalDictionaryChange<RuntimeEnvironmentNode>>? nodeChanges,
                out conflict)
            || !_registrations.TrySelectChanges(
                participant,
                registrationContributors,
                static key => new RuntimeEnvironmentRegistrationConflictKey(key),
                conflictStrategy,
                out Dictionary<string, TransactionalDictionaryChange<RuntimeEnvironmentId>>? registrationChanges,
                out conflict))
        {
            candidate = null;
            retainedEnvironmentIds = null;
            return false;
        }

        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> preliminaryNodes = _nodes.PrepareCandidate(nodeChanges);
        TransactionalDictionary<string, RuntimeEnvironmentId> mergedRegistrations = _registrations.PrepareCandidate(registrationChanges);
        retainedEnvironmentIds = DetermineRetainedEnvironmentIds(preliminaryNodes, mergedRegistrations);

        Dictionary<RuntimeEnvironmentId, TransactionalDictionaryChange<RuntimeEnvironmentNode>> retainedNodeChanges = [];
        foreach ((RuntimeEnvironmentId environmentId, TransactionalDictionaryChange<RuntimeEnvironmentNode> change) in nodeChanges)
        {
            if (change.Kind == TransactionalDictionaryChangeKind.Remove
                || retainedEnvironmentIds.Contains(environmentId))
            {
                retainedNodeChanges.Add(environmentId, change);
            }
        }

        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> mergedNodes = _nodes.PrepareCandidate(retainedNodeChanges);
        ValidateRegistrations(mergedNodes, mergedRegistrations);
        candidate = new RuntimeEnvironmentGraphState(mergedNodes, mergedRegistrations);
        conflict = null;
        return true;
    }

    private HashSet<RuntimeEnvironmentId> DetermineRetainedEnvironmentIds(
        TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode> nodes,
        TransactionalDictionary<string, RuntimeEnvironmentId> registrations)
    {
        HashSet<RuntimeEnvironmentId> retained = [.. _nodes.Baseline.Keys];
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

    private readonly record struct RuntimeEnvironmentRegistrationConflictKey(string Name);

    private readonly record struct RuntimeEnvironmentTopologyConflictKey(RuntimeEnvironmentId EnvironmentId);
}
