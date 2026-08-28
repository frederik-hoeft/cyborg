namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal interface ITransactionConflictStrategy
{
    TransactionConflictResolution Resolve(TransactionConflict conflict);
}
