namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

/// <summary>Selects a participant's contribution to reconciliation for one completed invocation.</summary>
internal interface ITransactionParticipantContributionPolicy
{
    ITransactionParticipantState SelectContribution(
        TransactionPublicationDisposition disposition,
        ITransactionParticipantFork fork,
        ITransactionParticipantState completedState);
}