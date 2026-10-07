using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Services.Transactions;

internal sealed class TransactionalServiceParticipantFork(TransactionalServiceParticipantAdapter participant, ITransactionalServiceForkAdapter fork) : ITransactionParticipantFork
{
    private readonly TransactionalServiceParticipantAdapter _participant = participant ?? throw new ArgumentNullException(nameof(participant));
    private readonly ITransactionalServiceForkAdapter _fork = fork ?? throw new ArgumentNullException(nameof(fork));

    public ITransactionParticipantState CreateBranch() => new TransactionalServiceParticipantState(_participant, _fork.CreateBranch());

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
        if (!ReferenceEquals(participant, _participant))
        {
            throw new InvalidOperationException("Transactional service fork was asked to reconcile a different participant descriptor.");
        }
        if (ownerContinuation is not TransactionalServiceParticipantState typedOwnerContinuation
            || !ReferenceEquals(typedOwnerContinuation.Participant, _participant))
        {
            throw new InvalidOperationException("Transactional service owner-continuation state does not belong to this participant.");
        }

        object[] childValues = new object[children.Count];
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not TransactionalServiceParticipantState child
                || !ReferenceEquals(child.Participant, _participant))
            {
                throw new InvalidOperationException("Transactional service child state does not belong to this participant.");
            }
            childValues[i] = child.Value;
        }

        TransactionalServiceConflictResolver resolver = new(_participant, conflictStrategy, children.Count + 1);
        if (!_fork.TryPrepareMerge(typedOwnerContinuation.Value, childValues, resolver, out object? candidateValue))
        {
            conflict = resolver.UnresolvedConflict
                ?? throw new InvalidOperationException(
                    $"Transactional service participant '{_participant.Participant.GetType().FullName}' returned a failed merge without reporting a conflict through the supplied resolver.");
            candidate = null;
            return false;
        }
        if (resolver.UnresolvedConflict is not null)
        {
            throw new InvalidOperationException(
                $"Transactional service participant '{_participant.Participant.GetType().FullName}' returned a successful merge after the configured conflict strategy rejected a reported conflict.");
        }
        candidate = new TransactionalServiceParticipantState(_participant, candidateValue);
        conflict = null;
        return true;
    }
}
