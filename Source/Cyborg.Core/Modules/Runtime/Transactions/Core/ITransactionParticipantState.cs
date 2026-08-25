namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal interface ITransactionParticipantState
{
    ITransactionParticipantFork CreateFork();
}
