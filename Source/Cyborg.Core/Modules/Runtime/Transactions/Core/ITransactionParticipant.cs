namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal interface ITransactionParticipant
{
    ITransactionParticipantState CreateRootState(TransactionRootSeed seed);
}

internal interface ITransactionParticipant<TState> : ITransactionParticipant
    where TState : ITransactionParticipantState
{
    new TState CreateRootState(TransactionRootSeed seed);

    ITransactionParticipantState ITransactionParticipant.CreateRootState(TransactionRootSeed seed) => CreateRootState(seed);
}
