using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.Transactions;

internal sealed class RuntimeTransactionalServices
{
    private readonly ImmutableDictionary<Type, TransactionalServiceParticipantAdapter> _participantsByType;

    public RuntimeTransactionalServices(IEnumerable<TransactionalServiceParticipant> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ImmutableArray<TransactionalServiceParticipantAdapter>.Builder adapters = ImmutableArray.CreateBuilder<TransactionalServiceParticipantAdapter>();
        ImmutableDictionary<Type, TransactionalServiceParticipantAdapter>.Builder participantsByType = ImmutableDictionary.CreateBuilder<Type, TransactionalServiceParticipantAdapter>();
        foreach (TransactionalServiceParticipant participant in participants)
        {
            ArgumentNullException.ThrowIfNull(participant);
            Type participantType = participant.GetType();
            if (participantsByType.ContainsKey(participantType))
            {
                throw new InvalidOperationException($"Transactional service participant type '{participantType.FullName}' is registered more than once.");
            }
            TransactionalServiceParticipantAdapter adapter = new(participant);
            adapters.Add(adapter);
            participantsByType.Add(participantType, adapter);
        }
        Participants = [.. adapters];
        _participantsByType = participantsByType.ToImmutable();
    }

    public ImmutableArray<ITransactionParticipant> Participants { get; }

    public void BindExecutionScope(IServiceProvider services, ModuleTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(transaction);
        ITransactionalServiceContext? context = services.GetService<ITransactionalServiceContext>();
        if (context is null)
        {
            if (Participants.IsEmpty)
            {
                return;
            }
            throw new InvalidOperationException("Transactional service participants require a scoped transactional service context in every module execution scope.");
        }
        if (context is not ITransactionBoundTransactionalServiceContext transactionBoundContext)
        {
            throw new InvalidOperationException(
                $"Configured transactional service context '{context.GetType().FullName}' does not support execution-transaction binding.");
        }
        transactionBoundContext.Bind(this, transaction);
    }

    public TState GetState<TParticipant, TState>(ModuleTransaction transaction)
        where TParticipant : TransactionalServiceParticipant<TState>
        where TState : class
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (!_participantsByType.TryGetValue(typeof(TParticipant), out TransactionalServiceParticipantAdapter? participant))
        {
            throw new InvalidOperationException($"Transactional service participant '{typeof(TParticipant).FullName}' is not registered with this execution runtime.");
        }
        TransactionalServiceParticipantState state = transaction.GetParticipantState(participant);
        if (state.Value is not TState typedState)
        {
            throw new InvalidOperationException(
                $"Transactional service participant '{typeof(TParticipant).FullName}' produced state type '{state.Value.GetType().FullName}' instead of '{typeof(TState).FullName}'.");
        }
        return typedState;
    }
}
