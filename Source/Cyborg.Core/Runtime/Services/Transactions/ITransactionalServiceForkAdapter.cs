namespace Cyborg.Core.Runtime.Services.Transactions;

internal interface ITransactionalServiceForkAdapter
{
    object CreateBranch();

    bool TryPrepareMerge(
        IReadOnlyList<object> contributors,
        ITransactionalServiceConflictResolver conflictResolver,
        [NotNullWhen(true)] out object? candidate);
}
