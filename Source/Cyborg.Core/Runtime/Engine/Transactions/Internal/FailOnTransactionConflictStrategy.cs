namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal sealed class FailOnTransactionConflictStrategy : ITransactionConflictStrategy
{
    public TransactionConflictResolution Resolve(TransactionConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        return TransactionConflictResolution.Fail();
    }
}
