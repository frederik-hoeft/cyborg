using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal sealed class TransactionCoordinator
{
    private readonly ITransactionConflictStrategy _conflictStrategy;
    private readonly ImmutableArray<ITransactionParticipant> _participants;

    public TransactionCoordinator(
        IEnumerable<ITransactionParticipant> participants,
        ITransactionConflictStrategy? conflictStrategy = null)
    {
        ArgumentNullException.ThrowIfNull(participants);
        _participants = [.. participants];
        _conflictStrategy = conflictStrategy ?? new FailOnTransactionConflictStrategy();
        ValidateParticipants(_participants);
    }

    internal ITransactionConflictStrategy ConflictStrategy => _conflictStrategy;

    internal ImmutableArray<ITransactionParticipant> Participants => _participants;

    public ExecutionTransaction CreateRoot() => CreateRoot(new TransactionRootSeed());

    public ExecutionTransaction CreateRoot(TransactionRootSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ImmutableDictionary<ITransactionParticipant, ITransactionParticipantState>.Builder states =
            ImmutableDictionary.CreateBuilder<ITransactionParticipant, ITransactionParticipantState>(ReferenceEqualityComparer.Instance);
        foreach (ITransactionParticipant participant in _participants)
        {
            ITransactionParticipantState state = participant.CreateRootState(seed)
                ?? throw new InvalidOperationException("A transaction participant returned a null root state.");
            states.Add(participant, state);
        }
        return new ExecutionTransaction(this, parent: null, ownerFork: null, new TransactionStateBundle(states.ToImmutable()));
    }

    private static void ValidateParticipants(ImmutableArray<ITransactionParticipant> participants)
    {
        HashSet<ITransactionParticipant> uniqueParticipants = new(ReferenceEqualityComparer.Instance);
        foreach (ITransactionParticipant participant in participants)
        {
            ArgumentNullException.ThrowIfNull(participant);
            if (!uniqueParticipants.Add(participant))
            {
                throw new ArgumentException("A transaction participant descriptor cannot be registered more than once.", nameof(participants));
            }
        }
    }
}
