namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal enum ExecutionTransactionForkLifecycle
{
    Active,
    Joined,
    Discarded,
    Conflict,
    Failed
}
