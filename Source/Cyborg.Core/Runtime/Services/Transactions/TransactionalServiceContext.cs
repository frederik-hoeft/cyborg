using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Services.Transactions;

internal sealed class TransactionalServiceContext : ITransactionalServiceContext, ITransactionBoundTransactionalServiceContext
{
    private RuntimeTransactionalServices? _services;
    private ExecutionTransaction? _transaction;

    public ITransactionalServiceState<TState> GetState<TParticipant, TState>()
        where TParticipant : TransactionalServiceParticipant<TState>
        where TState : class =>
        new TransactionalServiceState<TParticipant, TState>(this);

    internal TState ResolveState<TParticipant, TState>()
        where TParticipant : TransactionalServiceParticipant<TState>
        where TState : class
    {
        RuntimeTransactionalServices services = _services
            ?? throw new InvalidOperationException("Transactional service state can only be accessed from a module execution scope.");
        ExecutionTransaction transaction = _transaction
            ?? throw new InvalidOperationException("Transactional service state can only be accessed from a module execution scope.");
        return services.GetState<TParticipant, TState>(transaction);
    }

    void ITransactionBoundTransactionalServiceContext.Bind(RuntimeTransactionalServices services, ExecutionTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(transaction);
        if (_transaction is not null)
        {
            throw new InvalidOperationException("The transactional service context is already bound to an execution transaction.");
        }
        _services = services;
        _transaction = transaction;
    }
}
