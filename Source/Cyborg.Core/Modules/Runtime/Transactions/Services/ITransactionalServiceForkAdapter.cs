using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions.Services;

internal interface ITransactionalServiceForkAdapter
{
    object CreateBranch();

    bool TryPrepareMerge(
        IReadOnlyList<object> contributors,
        ITransactionalServiceConflictResolver conflictResolver,
        [NotNullWhen(true)] out object? candidate);
}
