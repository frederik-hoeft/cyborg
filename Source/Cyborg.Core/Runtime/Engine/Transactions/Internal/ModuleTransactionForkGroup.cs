using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal sealed class ModuleTransactionForkGroup
{
    private readonly TransactionCoordinator _coordinator;
    private readonly ImmutableDictionary<ITransactionParticipant, ITransactionParticipantFork> _participantForks;
    private readonly List<ModuleTransaction> _children = [];
    private readonly ModuleTransaction _owner;

    public ModuleTransactionForkGroup(ModuleTransaction owner, TransactionCoordinator coordinator, TransactionStateBundle ownerState)
    {
        _owner = owner;
        _coordinator = coordinator;
        _participantForks = CreateParticipantForks(coordinator.Participants, ownerState);
        Continuation = CreateBranch();
        Lifecycle = ModuleTransactionForkLifecycle.Active;
    }

    public ModuleTransaction Continuation { get; }

    public ModuleTransactionForkLifecycle Lifecycle { get; private set; }

    public IReadOnlyList<ModuleTransaction> Children => _children;

    public ModuleTransaction CreateChild()
    {
        EnsureActive();
        ModuleTransaction child = CreateBranch();
        _children.Add(child);
        return child;
    }

    public bool TryJoin(out TransactionConflict? conflict)
    {
        EnsureActive();
        List<ModuleTransaction> contributors = [Continuation, .. _children];
        List<TransactionStateBundle> contributorStates = new(contributors.Count);
        foreach (ModuleTransaction contributor in contributors)
        {
            contributorStates.Add(contributor.GetStateForReconciliation(this));
        }

        ImmutableDictionary<ITransactionParticipant, ITransactionParticipantState>.Builder candidates =
            ImmutableDictionary.CreateBuilder<ITransactionParticipant, ITransactionParticipantState>(ReferenceEqualityComparer.Instance);
        foreach (ITransactionParticipant participant in _coordinator.Participants)
        {
            ITransactionParticipantFork participantFork = _participantForks[participant];
            ITransactionParticipantState[] participantContributors = new ITransactionParticipantState[contributorStates.Count];
            for (int i = 0; i < contributorStates.Count; i++)
            {
                participantContributors[i] = contributorStates[i].Get(participant);
            }

            ITransactionParticipantState? candidate;
            try
            {
                if (!participantFork.TryPrepareMerge(
                    participant,
                    participantContributors,
                    _coordinator.ConflictStrategy,
                    out candidate,
                    out conflict))
                {
                    CloseFork(contributors, ModuleTransactionForkLifecycle.Conflict);
                    return false;
                }
            }
            catch
            {
                CloseFork(contributors, ModuleTransactionForkLifecycle.Failed);
                throw;
            }
            candidates.Add(participant, candidate);
        }

        TransactionStateBundle candidateState = new(candidates.ToImmutable());
        _owner.PublishForkResult(this, candidateState);
        foreach (ModuleTransaction contributor in contributors)
        {
            contributor.MarkJoined(this);
        }
        Lifecycle = ModuleTransactionForkLifecycle.Joined;
        conflict = null;
        return true;
    }

    public void Discard()
    {
        EnsureActive();
        List<ModuleTransaction> contributors = [Continuation, .. _children];
        EnsureContributorsCanBeDiscarded(contributors);
        foreach (ModuleTransaction contributor in contributors)
        {
            contributor.MarkDiscardedByFork(this);
        }
        _owner.ReleaseForkWithoutPublication(this);
        Lifecycle = ModuleTransactionForkLifecycle.Discarded;
    }

    private ModuleTransaction CreateBranch()
    {
        ImmutableDictionary<ITransactionParticipant, ITransactionParticipantState>.Builder states = ImmutableDictionary.CreateBuilder<ITransactionParticipant, ITransactionParticipantState>(ReferenceEqualityComparer.Instance);
        foreach (ITransactionParticipant participant in _coordinator.Participants)
        {
            ITransactionParticipantState state = _participantForks[participant].CreateBranch()
                ?? throw new InvalidOperationException("A transaction participant returned a null branch state.");
            states.Add(participant, state);
        }
        return new ModuleTransaction(_coordinator, _owner, this, new TransactionStateBundle(states.ToImmutable()));
    }

    private void CloseFork(
        IReadOnlyCollection<ModuleTransaction> contributors,
        ModuleTransactionForkLifecycle lifecycle)
    {
        EnsureContributorsCanBeDiscarded(contributors);
        foreach (ModuleTransaction contributor in contributors)
        {
            contributor.MarkDiscardedByFork(this);
        }
        _owner.ReleaseForkWithoutPublication(this);
        Lifecycle = lifecycle;
    }

    private void EnsureContributorsCanBeDiscarded(IReadOnlyCollection<ModuleTransaction> contributors)
    {
        foreach (ModuleTransaction contributor in contributors)
        {
            contributor.EnsureCanBeDiscardedByFork(this);
        }
    }

    private void EnsureActive()
    {
        if (Lifecycle != ModuleTransactionForkLifecycle.Active)
        {
            throw new InvalidOperationException($"Fork group state is '{Lifecycle}' and cannot be used for this operation.");
        }
    }

    private static ImmutableDictionary<ITransactionParticipant, ITransactionParticipantFork> CreateParticipantForks(
        ImmutableArray<ITransactionParticipant> participants,
        TransactionStateBundle ownerState)
    {
        ImmutableDictionary<ITransactionParticipant, ITransactionParticipantFork>.Builder forks =
            ImmutableDictionary.CreateBuilder<ITransactionParticipant, ITransactionParticipantFork>(ReferenceEqualityComparer.Instance);
        foreach (ITransactionParticipant participant in participants)
        {
            ITransactionParticipantFork fork = ownerState.Get(participant).CreateFork()
                ?? throw new InvalidOperationException("A transaction participant returned a null fork.");
            forks.Add(participant, fork);
        }
        return forks.ToImmutable();
    }
}
