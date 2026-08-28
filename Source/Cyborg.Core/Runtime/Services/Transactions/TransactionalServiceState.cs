namespace Cyborg.Core.Runtime.Services.Transactions;

internal sealed class TransactionalServiceState<TParticipant, TState>(TransactionalServiceContext context) : ITransactionalServiceState<TState>
    where TParticipant : TransactionalServiceParticipant<TState>
    where TState : class
{
    private readonly TransactionalServiceContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public TResult Read<TResult>(Func<TState, TResult> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader(_context.ResolveState<TParticipant, TState>());
    }

    public void Mutate(Action<TState> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        mutation(_context.ResolveState<TParticipant, TState>());
    }
}
