namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal interface ITransactionParticipantState
{
    ITransactionParticipantFork CreateFork();
}
