namespace Cyborg.Core.Runtime.Services.Transactions;

internal interface ITransactionalServiceForkAdapter
{
    object CreateBranch();

    bool TryPrepareMerge(
        object ownerContinuation,
        IReadOnlyList<object> children,
        ITransactionalServiceConflictResolver conflictResolver,
        [NotNullWhen(true)] out object? candidate);
}
