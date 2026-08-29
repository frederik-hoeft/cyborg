namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal interface ITransactionParticipant<TState> : ITransactionParticipant
    where TState : ITransactionParticipantState
{
    new TState CreateRootState(TransactionRootSeed seed);

    ITransactionParticipantState ITransactionParticipant.CreateRootState(TransactionRootSeed seed) => CreateRootState(seed);
}
