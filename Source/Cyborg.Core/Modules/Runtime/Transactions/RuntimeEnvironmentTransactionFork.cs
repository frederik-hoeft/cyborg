using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class RuntimeEnvironmentTransactionFork : ITransactionParticipantFork
{
    private readonly RuntimeEnvironmentBindingFork _bindings;
    private readonly RuntimeEnvironmentGraphFork _graph;
    private readonly RuntimeEnvironmentTransactionState _owner;

    public RuntimeEnvironmentTransactionFork(
        RuntimeEnvironmentTransactionState owner,
        RuntimeEnvironmentGraphFork graph,
        RuntimeEnvironmentBindingFork bindings)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(bindings);
        _owner = owner;
        _graph = graph;
        _bindings = bindings;
    }

    public ITransactionParticipantState CreateBranch() => new RuntimeEnvironmentTransactionState(
        _owner.GlobalEnvironmentId,
        _graph.CreateBranch(),
        _bindings.CreateBranch());

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

        RuntimeEnvironmentGraphState[] graphContributors = new RuntimeEnvironmentGraphState[contributors.Count];
        RuntimeEnvironmentBindingState[] bindingContributors = new RuntimeEnvironmentBindingState[contributors.Count];
        for (int i = 0; i < contributors.Count; i++)
        {
            RuntimeEnvironmentTransactionState contributor = (RuntimeEnvironmentTransactionState)contributors[i];
            graphContributors[i] = contributor.Graph;
            bindingContributors[i] = contributor.Bindings;
        }

        if (!_graph.TryPrepareMerge(
                participant,
                graphContributors,
                conflictStrategy,
                out RuntimeEnvironmentGraphState? graphCandidate,
                out HashSet<RuntimeEnvironmentId>? retainedEnvironmentIds,
                out conflict)
            || !_bindings.TryPrepareMerge(
                participant,
                bindingContributors,
                retainedEnvironmentIds,
                conflictStrategy,
                out RuntimeEnvironmentBindingState? bindingCandidate,
                out conflict))
        {
            candidate = null;
            return false;
        }

        candidate = new RuntimeEnvironmentTransactionState(
            _owner.GlobalEnvironmentId,
            graphCandidate,
            bindingCandidate);
        conflict = null;
        return true;
    }
}
