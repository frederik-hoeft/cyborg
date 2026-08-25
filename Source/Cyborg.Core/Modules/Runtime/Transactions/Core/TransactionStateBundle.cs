using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal sealed class TransactionStateBundle
{
    private readonly ImmutableDictionary<ITransactionParticipant, ITransactionParticipantState> _states;

    internal TransactionStateBundle(ImmutableDictionary<ITransactionParticipant, ITransactionParticipantState> states)
    {
        _states = states;
    }

    internal TState Get<TState>(ITransactionParticipant<TState> participant)
        where TState : ITransactionParticipantState
    {
        ArgumentNullException.ThrowIfNull(participant);
        if (!_states.TryGetValue(participant, out ITransactionParticipantState? state))
        {
            throw new KeyNotFoundException("The requested participant is not registered with this transaction coordinator.");
        }
        if (state is not TState typedState)
        {
            throw new InvalidOperationException(
                $"Participant state type '{state.GetType()}' does not match requested state type '{typeof(TState)}'.");
        }
        return typedState;
    }

    internal ITransactionParticipantState Get(ITransactionParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        if (!_states.TryGetValue(participant, out ITransactionParticipantState? state))
        {
            throw new KeyNotFoundException("The requested participant is not registered with this transaction coordinator.");
        }
        return state;
    }
}
