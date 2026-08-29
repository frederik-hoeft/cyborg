using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal sealed class TransactionCoordinator(IEnumerable<ITransactionParticipant> participants, ITransactionConflictStrategy? conflictStrategy = null)
{
    internal ITransactionConflictStrategy ConflictStrategy { get; } = conflictStrategy ?? new FailOnTransactionConflictStrategy();

    internal ImmutableArray<ITransactionParticipant> Participants { get; } = ValidateParticipants(participants);

    public ModuleTransaction CreateRoot() => CreateRoot(new TransactionRootSeed());

    public ModuleTransaction CreateRoot(TransactionRootSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ImmutableDictionary<ITransactionParticipant, ITransactionParticipantState>.Builder states = ImmutableDictionary
            .CreateBuilder<ITransactionParticipant, ITransactionParticipantState>(ReferenceEqualityComparer.Instance);
        foreach (ITransactionParticipant participant in Participants)
        {
            ITransactionParticipantState state = participant.CreateRootState(seed)
                ?? throw new InvalidOperationException("A transaction participant returned a null root state.");
            states.Add(participant, state);
        }
        return new ModuleTransaction(coordinator: this, parent: null, ownerFork: null, new TransactionStateBundle(states.ToImmutable()));
    }

    private static ImmutableArray<ITransactionParticipant> ValidateParticipants(IEnumerable<ITransactionParticipant> participants)
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
        return [.. uniqueParticipants];
    }
}
