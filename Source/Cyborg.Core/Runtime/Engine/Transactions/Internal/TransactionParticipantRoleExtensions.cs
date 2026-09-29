using Cyborg.Core.Runtime.Services.Transactions;

namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

/// <summary>Maps participant roles to their contribution policies without coupling reconciliation to role semantics.</summary>
internal static class TransactionParticipantRoleExtensions
{
    private static readonly ITransactionParticipantContributionPolicy s_workflowData = new WorkflowDataContributionPolicy();
    private static readonly ITransactionParticipantContributionPolicy s_control = new ControlContributionPolicy();

    public static ITransactionParticipantContributionPolicy GetContributionPolicy(this TransactionParticipantRole role) => role switch
    {
        TransactionParticipantRole.WorkflowData => s_workflowData,
        TransactionParticipantRole.Control => s_control,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported transaction participant role."),
    };

    private sealed class WorkflowDataContributionPolicy : ITransactionParticipantContributionPolicy
    {
        public ITransactionParticipantState SelectContribution(
            TransactionPublicationDisposition disposition,
            ITransactionParticipantFork fork,
            ITransactionParticipantState completedState) => disposition switch
        {
            TransactionPublicationDisposition.Commit => completedState,
            TransactionPublicationDisposition.Rollback => fork.CreateBranch()
                ?? throw new InvalidOperationException("A transaction participant returned a null branch state."),
            _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, "Unsupported transaction publication disposition."),
        };
    }

    private sealed class ControlContributionPolicy : ITransactionParticipantContributionPolicy
    {
        public ITransactionParticipantState SelectContribution(
            TransactionPublicationDisposition disposition,
            ITransactionParticipantFork fork,
            ITransactionParticipantState completedState) => disposition switch
        {
            TransactionPublicationDisposition.Commit or TransactionPublicationDisposition.Rollback => completedState,
            _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, "Unsupported transaction publication disposition."),
        };
    }
}