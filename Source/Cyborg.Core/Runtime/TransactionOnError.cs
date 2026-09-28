namespace Cyborg.Core.Runtime;

/// <summary>Selects how a failed or canceled invocation publishes transactional workflow data.</summary>
public enum TransactionOnError
{
    /// <summary>Join the completed child transaction into its parent. This is the historical behavior.</summary>
    Commit,

    /// <summary>Reconcile control state, but publish the fork baseline for workflow-data participants.</summary>
    Rollback,
}
