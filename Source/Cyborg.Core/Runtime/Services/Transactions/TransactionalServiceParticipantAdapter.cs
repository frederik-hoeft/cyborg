using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Services.Transactions;

internal sealed class TransactionalServiceParticipantAdapter(TransactionalServiceParticipant participant)
    : ITransactionParticipant<TransactionalServiceParticipantState>
{
    public TransactionalServiceParticipant Participant { get; } = participant ?? throw new ArgumentNullException(nameof(participant));

    public TransactionalServiceParticipantState CreateRootState(TransactionRootSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        return new TransactionalServiceParticipantState(this, Participant.CreateRootStateCore());
    }
}
