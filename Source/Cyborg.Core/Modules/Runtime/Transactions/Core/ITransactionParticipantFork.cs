using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal interface ITransactionParticipantFork
{
    ITransactionParticipantState CreateBranch();

    bool TryPrepareMerge(
        ITransactionParticipant participant,
        IReadOnlyList<ITransactionParticipantState> contributors,
        ITransactionConflictStrategy conflictStrategy,
        [NotNullWhen(true)] out ITransactionParticipantState? candidate,
        out TransactionConflict? conflict);
}
