namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal interface ITransactionParticipantFork
{
    ITransactionParticipantState CreateBranch();

    bool TryPrepareMerge(
        ITransactionParticipant participant,
        ITransactionParticipantState ownerContinuation,
        IReadOnlyList<ITransactionParticipantState> children,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out ITransactionParticipantState? candidate,
        [NotNullWhen(false)] out TransactionConflict? conflict);
}
