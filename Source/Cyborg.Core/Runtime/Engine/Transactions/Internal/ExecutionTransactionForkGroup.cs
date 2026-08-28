using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal sealed class ExecutionTransactionForkGroup
{
    private readonly TransactionCoordinator _coordinator;
    private readonly ImmutableDictionary<ITransactionParticipant, ITransactionParticipantFork> _participantForks;
    private readonly List<ExecutionTransaction> _children = [];
    private readonly ExecutionTransaction _owner;

    public ExecutionTransactionForkGroup(ExecutionTransaction owner, TransactionCoordinator coordinator, TransactionStateBundle ownerState)
    {
        _owner = owner;
        _coordinator = coordinator;
        _participantForks = CreateParticipantForks(coordinator.Participants, ownerState);
        Continuation = CreateBranch();
        Lifecycle = ExecutionTransactionForkLifecycle.Active;
    }

    public ExecutionTransaction Continuation { get; }

    public ExecutionTransactionForkLifecycle Lifecycle { get; private set; }

    public IReadOnlyList<ExecutionTransaction> Children => _children;

    public ExecutionTransaction CreateChild()
    {
        EnsureActive();
        ExecutionTransaction child = CreateBranch();
        _children.Add(child);
        return child;
    }

    public bool TryJoin(out TransactionConflict? conflict)
    {
        EnsureActive();
        List<ExecutionTransaction> contributors = [Continuation, .. _children];
        List<TransactionStateBundle> contributorStates = new(contributors.Count);
        foreach (ExecutionTransaction contributor in contributors)
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
                    CloseFork(contributors, ExecutionTransactionForkLifecycle.Conflict);
                    return false;
                }
            }
            catch
            {
                CloseFork(contributors, ExecutionTransactionForkLifecycle.Failed);
                throw;
            }
            candidates.Add(participant, candidate);
        }

        TransactionStateBundle candidateState = new(candidates.ToImmutable());
        _owner.PublishForkResult(this, candidateState);
        foreach (ExecutionTransaction contributor in contributors)
        {
            contributor.MarkJoined(this);
        }
        Lifecycle = ExecutionTransactionForkLifecycle.Joined;
        conflict = null;
        return true;
    }

    public void Discard()
    {
        EnsureActive();
        List<ExecutionTransaction> contributors = [Continuation, .. _children];
        EnsureContributorsCanBeDiscarded(contributors);
        foreach (ExecutionTransaction contributor in contributors)
        {
            contributor.MarkDiscardedByFork(this);
        }
        _owner.ReleaseForkWithoutPublication(this);
        Lifecycle = ExecutionTransactionForkLifecycle.Discarded;
    }

    private ExecutionTransaction CreateBranch()
    {
        ImmutableDictionary<ITransactionParticipant, ITransactionParticipantState>.Builder states = ImmutableDictionary.CreateBuilder<ITransactionParticipant, ITransactionParticipantState>(ReferenceEqualityComparer.Instance);
        foreach (ITransactionParticipant participant in _coordinator.Participants)
        {
            ITransactionParticipantState state = _participantForks[participant].CreateBranch()
                ?? throw new InvalidOperationException("A transaction participant returned a null branch state.");
            states.Add(participant, state);
        }
        return new ExecutionTransaction(_coordinator, _owner, this, new TransactionStateBundle(states.ToImmutable()));
    }

    private void CloseFork(
        IReadOnlyCollection<ExecutionTransaction> contributors,
        ExecutionTransactionForkLifecycle lifecycle)
    {
        EnsureContributorsCanBeDiscarded(contributors);
        foreach (ExecutionTransaction contributor in contributors)
        {
            contributor.MarkDiscardedByFork(this);
        }
        _owner.ReleaseForkWithoutPublication(this);
        Lifecycle = lifecycle;
    }

    private void EnsureContributorsCanBeDiscarded(IReadOnlyCollection<ExecutionTransaction> contributors)
    {
        foreach (ExecutionTransaction contributor in contributors)
        {
            contributor.EnsureCanBeDiscardedByFork(this);
        }
    }

    private void EnsureActive()
    {
        if (Lifecycle != ExecutionTransactionForkLifecycle.Active)
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
