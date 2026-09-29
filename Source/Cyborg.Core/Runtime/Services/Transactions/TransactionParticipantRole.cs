namespace Cyborg.Core.Runtime.Services.Transactions;

/// <summary>Classifies a transaction participant for failure publication.</summary>
public enum TransactionParticipantRole
{
    /// <summary>Workflow-semantic state. Rollback replaces it with the fork baseline.</summary>
    WorkflowData,

    /// <summary>Execution-control state. Rollback still reconciles it into the owner.</summary>
    Control,
}
