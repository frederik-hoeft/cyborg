using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal sealed class TransactionStateBundle(ImmutableDictionary<ITransactionParticipant, ITransactionParticipantState> states)
{
    internal TState Get<TState>(ITransactionParticipant<TState> participant) where TState : ITransactionParticipantState
    {
        ArgumentNullException.ThrowIfNull(participant);
        if (!states.TryGetValue(participant, out ITransactionParticipantState? state))
        {
            throw new KeyNotFoundException("The requested participant is not registered with this transaction coordinator.");
        }
        if (state is not TState typedState)
        {
            throw new InvalidOperationException($"Participant state type '{state.GetType()}' does not match requested state type '{typeof(TState)}'.");
        }
        return typedState;
    }

    internal ITransactionParticipantState Get(ITransactionParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        if (!states.TryGetValue(participant, out ITransactionParticipantState? state))
        {
            throw new KeyNotFoundException("The requested participant is not registered with this transaction coordinator.");
        }
        return state;
    }
}
