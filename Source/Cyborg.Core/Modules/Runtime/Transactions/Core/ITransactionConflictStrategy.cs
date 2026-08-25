namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal interface ITransactionConflictStrategy
{
    TransactionConflictResolution Resolve(TransactionConflict conflict);
}
