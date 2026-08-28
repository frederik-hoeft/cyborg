namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal interface ITransactionParticipant
{
    ITransactionParticipantState CreateRootState(TransactionRootSeed seed);
}
