namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal interface ITransactionParticipantFork
{
    ITransactionParticipantState CreateBranch();

    bool TryPrepareMerge(ITransactionParticipant participant,
        IReadOnlyList<ITransactionParticipantState> contributors,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out ITransactionParticipantState? candidate,
        out TransactionConflict? conflict);
}
