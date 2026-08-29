namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal enum ModuleTransactionForkLifecycle
{
    Active,
    Joined,
    Discarded,
    Conflict,
    Failed
}
