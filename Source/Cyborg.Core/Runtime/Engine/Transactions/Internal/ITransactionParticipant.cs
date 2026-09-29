using Cyborg.Core.Runtime.Services.Transactions;

namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal interface ITransactionParticipant
{
    /// <summary>Workflow data by default. Control participants still reconcile when workflow data is rolled back.</summary>
    TransactionParticipantRole Role => TransactionParticipantRole.WorkflowData;

    /// <summary>Selects this participant's reconciliation contribution independently of the fork group.</summary>
    ITransactionParticipantContributionPolicy ContributionPolicy => Role.GetContributionPolicy();

    ITransactionParticipantState CreateRootState(TransactionRootSeed seed);
}
