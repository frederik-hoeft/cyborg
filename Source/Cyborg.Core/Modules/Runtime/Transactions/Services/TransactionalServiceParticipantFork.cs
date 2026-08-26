using Cyborg.Core.Modules.Runtime.Transactions.Core;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions.Services;

internal sealed class TransactionalServiceParticipantFork(
    TransactionalServiceParticipantAdapter participant,
    ITransactionalServiceForkAdapter fork) : ITransactionParticipantFork
{
    private readonly TransactionalServiceParticipantAdapter _participant = participant ?? throw new ArgumentNullException(nameof(participant));
    private readonly ITransactionalServiceForkAdapter _fork = fork ?? throw new ArgumentNullException(nameof(fork));

    public ITransactionParticipantState CreateBranch() =>
        new TransactionalServiceParticipantState(_participant, _fork.CreateBranch());

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
        if (!ReferenceEquals(participant, _participant))
        {
            throw new InvalidOperationException("Transactional service fork was asked to reconcile a different participant descriptor.");
        }

        object[] values = new object[contributors.Count];
        for (int i = 0; i < contributors.Count; i++)
        {
            if (contributors[i] is not TransactionalServiceParticipantState contributor
                || !ReferenceEquals(contributor.Participant, _participant))
            {
                throw new InvalidOperationException("Transactional service contributor state does not belong to this participant.");
            }
            values[i] = contributor.Value;
        }

        TransactionalServiceConflictResolver resolver = new(_participant, conflictStrategy, contributors.Count);
        if (!_fork.TryPrepareMerge(values, resolver, out object? candidateValue))
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
