using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Services.Transactions;

internal sealed class TransactionalServiceParticipantState(
    TransactionalServiceParticipantAdapter participant,
    object value) : ITransactionParticipantState
{
    public TransactionalServiceParticipantAdapter Participant { get; } = participant ?? throw new ArgumentNullException(nameof(participant));

    public object Value { get; } = value ?? throw new ArgumentNullException(nameof(value));

    public ITransactionParticipantFork CreateFork() =>
        new TransactionalServiceParticipantFork(Participant, Participant.Participant.CreateForkCore(Value));
}
