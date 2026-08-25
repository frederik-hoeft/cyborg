namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal sealed class FailOnTransactionConflictStrategy : ITransactionConflictStrategy
{
    public FailOnTransactionConflictStrategy()
    {
    }

    public TransactionConflictResolution Resolve(TransactionConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        return TransactionConflictResolution.Fail();
    }
}
