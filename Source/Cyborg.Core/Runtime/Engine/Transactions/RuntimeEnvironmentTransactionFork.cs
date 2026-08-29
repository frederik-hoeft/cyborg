using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine.Transactions;

internal sealed class RuntimeEnvironmentTransactionFork
(
    RuntimeEnvironmentTransactionState owner,
    RuntimeEnvironmentGraphFork graph,
    RuntimeEnvironmentBindingFork bindings
) : ITransactionParticipantFork
{
    public ITransactionParticipantState CreateBranch() => new RuntimeEnvironmentTransactionState(owner.GlobalEnvironmentId, graph.CreateBranch(), bindings.CreateBranch());

    public bool TryPrepareMerge(ITransactionParticipant participant,
        IReadOnlyList<ITransactionParticipantState> contributors,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out ITransactionParticipantState? candidate,
        [NotNullWhen(false)] out TransactionConflict? conflict)
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

        if (!graph.TryPrepareMerge(participant, graphContributors, conflictStrategy, out RuntimeEnvironmentGraphState? graphCandidate, out HashSet<RuntimeEnvironmentId>? retainedEnvironmentIds, out conflict)
            || !bindings.TryPrepareMerge(participant, bindingContributors, retainedEnvironmentIds, conflictStrategy, out RuntimeEnvironmentBindingState? bindingCandidate, out conflict))
        {
            candidate = null;
            return false;
        }

        candidate = new RuntimeEnvironmentTransactionState(owner.GlobalEnvironmentId, graphCandidate, bindingCandidate);
        conflict = null;
        return true;
    }
}
