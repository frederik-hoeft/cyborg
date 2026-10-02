namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

/// <summary>
/// Invocation-scoped pointer to the transaction that currently receives that invocation's reads and writes.
/// A concurrent execution scope retargets it at the fork continuation and restores the owner after the fork closes.
/// </summary>
internal sealed class ActiveTransaction
{
    private ModuleTransaction _transaction;

    public ActiveTransaction(ModuleTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        _transaction = transaction;
    }

    public ModuleTransaction Current => Volatile.Read(ref _transaction);

    public void Retarget(ModuleTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        Volatile.Write(ref _transaction, transaction);
    }
}
