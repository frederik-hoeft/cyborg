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

    public bool TryPrepareMerge(
        ITransactionParticipant participant,
        ITransactionParticipantState ownerContinuation,
        IReadOnlyList<ITransactionParticipantState> children,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out ITransactionParticipantState? candidate,
        [NotNullWhen(false)] out TransactionConflict? conflict)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(ownerContinuation);
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(conflictStrategy);

        RuntimeEnvironmentGraphState[] graphContributors = new RuntimeEnvironmentGraphState[children.Count + 1];
        RuntimeEnvironmentBindingState[] bindingContributors = new RuntimeEnvironmentBindingState[children.Count + 1];
        RuntimeEnvironmentTransactionState continuation = (RuntimeEnvironmentTransactionState)ownerContinuation;
        graphContributors[0] = continuation.Graph;
        bindingContributors[0] = continuation.Bindings;
        for (int i = 0; i < children.Count; i++)
        {
            RuntimeEnvironmentTransactionState child = (RuntimeEnvironmentTransactionState)children[i];
            graphContributors[i + 1] = child.Graph;
            bindingContributors[i + 1] = child.Bindings;
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
