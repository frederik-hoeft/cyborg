namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal enum ExecutionTransactionForkLifecycle
{
    Active,
    Joined,
    Discarded,
    Conflict,
    Failed
}
