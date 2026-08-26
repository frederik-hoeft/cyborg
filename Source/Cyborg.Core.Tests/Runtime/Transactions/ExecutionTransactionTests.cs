using System.Diagnostics.CodeAnalysis;
using Cyborg.Core.Modules.Runtime.Transactions.Collections;
using Cyborg.Core.Modules.Runtime.Transactions.Core;

namespace Cyborg.Core.Tests.Runtime.Transactions;

[TestClass]
public sealed class ExecutionTransactionTests
{
    [TestMethod]
    public void CreateRoot_MultipleRootsHaveIndependentParticipantState()
    {
        DictionaryParticipant participant = new();
        TransactionCoordinator coordinator = new([participant]);
        ExecutionTransaction first = coordinator.CreateRoot();
        ExecutionTransaction second = coordinator.CreateRoot();

        first.GetParticipantState(participant).Set("value", 1);

        Assert.IsTrue(first.IsRoot);
        Assert.IsTrue(second.IsRoot);
        Assert.IsNull(first.Parent);
        Assert.IsFalse(second.GetParticipantState(participant).ContainsKey("value"));
    }

    [TestMethod]
    public void CreateRoot_OneCoordinatorCanApplyDifferentImmutableExecutionSeeds()
    {
        DictionaryParticipant participant = new();
        TransactionCoordinator coordinator = new([participant]);
        KeyValuePair<string, int>[] firstValues = [new("seed", 1)];
        KeyValuePair<string, int>[] secondValues = [new("seed", 2)];
        TransactionRootSeed firstSeed = new TransactionRootSeed().With(participant, firstValues);
        TransactionRootSeed secondSeed = new TransactionRootSeed().With(participant, secondValues);

        ExecutionTransaction first = coordinator.CreateRoot(firstSeed);
        ExecutionTransaction second = coordinator.CreateRoot(secondSeed);

        Assert.AreEqual(1, first.GetParticipantState(participant)["seed"]);
        Assert.AreEqual(2, second.GetParticipantState(participant)["seed"]);
    }

    [TestMethod]
    public void Fork_ContinuationAndChildrenShareStableBaselineAndRemainIsolated()
    {
        DictionaryParticipant participant = new(("baseline", 1));
        TransactionCoordinator coordinator = new([participant]);
        ExecutionTransaction root = coordinator.CreateRoot();
        root.GetParticipantState(participant).Set("before-fork", 2);

        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction first = fork.CreateChild();
        ExecutionTransaction second = fork.CreateChild();
        DictionaryParticipantState continuationState = fork.Continuation.GetParticipantState(participant);
        DictionaryParticipantState firstState = first.GetParticipantState(participant);
        DictionaryParticipantState secondState = second.GetParticipantState(participant);
        firstState.Set("branch", 10);
        secondState.Set("branch", 20);

        Assert.AreSame(continuationState.Baseline, firstState.Baseline);
        Assert.AreSame(firstState.Baseline, secondState.Baseline);
        Assert.AreEqual(2, firstState["before-fork"]);
        Assert.AreEqual(10, firstState["branch"]);
        Assert.AreEqual(20, secondState["branch"]);
        Assert.IsFalse(continuationState.ContainsKey("branch"));
    }

    [TestMethod]
    public void Fork_OwnerStateIsUnavailableUntilForkCloses()
    {
        DictionaryParticipant participant = new();
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();

        ExecutionTransactionForkGroup fork = root.Fork();

        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetParticipantState(participant));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.Fork());
        Assert.ThrowsExactly<InvalidOperationException>(root.Complete);
        fork.Discard();
        Assert.AreEqual(ExecutionTransactionLifecycle.Active, root.Lifecycle);
        Assert.IsNotNull(root.GetParticipantState(participant));
    }

    [TestMethod]
    public void TryJoin_NonOverlappingContinuationAndChildChangesPublishAtomically()
    {
        DictionaryParticipant participant = new();
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction child = fork.CreateChild();
        fork.Continuation.GetParticipantState(participant).Set("continuation", 1);
        child.GetParticipantState(participant).Set("child", 2);
        fork.Continuation.Complete();
        child.Complete();

        bool joined = fork.TryJoin(out TransactionConflict? conflict);

        Assert.IsTrue(joined, conflict?.LogicalKey.ToString());
        Assert.IsNull(conflict);
        DictionaryParticipantState state = root.GetParticipantState(participant);
        Assert.AreEqual(1, state["continuation"]);
        Assert.AreEqual(2, state["child"]);
        Assert.AreEqual(ExecutionTransactionForkLifecycle.Joined, fork.Lifecycle);
        Assert.AreEqual(ExecutionTransactionLifecycle.Joined, fork.Continuation.Lifecycle);
        Assert.AreEqual(ExecutionTransactionLifecycle.Joined, child.Lifecycle);
    }

    [TestMethod]
    public void TryJoin_SiblingWriteConflictLeavesOwnerUnchanged()
    {
        DictionaryParticipant participant = new(("baseline", 1));
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction first = fork.CreateChild();
        ExecutionTransaction second = fork.CreateChild();
        first.GetParticipantState(participant).Set("value", 2);
        second.GetParticipantState(participant).Set("value", 2);
        fork.Continuation.Complete();
        first.Complete();
        second.Complete();

        bool joined = fork.TryJoin(out TransactionConflict? conflict);

        Assert.IsFalse(joined);
        Assert.IsNotNull(conflict);
        Assert.AreSame(participant, conflict.Participant);
        Assert.AreEqual("value", conflict.LogicalKey);
        Assert.AreEqual(2, conflict.ContributorIndices.Length);
        Assert.AreEqual(1, conflict.ContributorIndices[0]);
        Assert.AreEqual(2, conflict.ContributorIndices[1]);
        Assert.AreEqual(1, root.GetParticipantState(participant)["baseline"]);
        Assert.IsFalse(root.GetParticipantState(participant).ContainsKey("value"));
        Assert.AreEqual(ExecutionTransactionForkLifecycle.Conflict, fork.Lifecycle);
        Assert.AreEqual(ExecutionTransactionLifecycle.Discarded, first.Lifecycle);
        Assert.AreEqual(ExecutionTransactionLifecycle.Discarded, second.Lifecycle);
    }


    [TestMethod]
    public void TryJoin_AddThenRemoveStillParticipatesInConflictDetection()
    {
        DictionaryParticipant participant = new();
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction first = fork.CreateChild();
        ExecutionTransaction second = fork.CreateChild();
        DictionaryParticipantState firstState = first.GetParticipantState(participant);
        firstState.Set("value", 1);
        Assert.IsTrue(firstState.TryRemove("value"));
        second.GetParticipantState(participant).Set("value", 2);
        fork.Continuation.Complete();
        first.Complete();
        second.Complete();

        bool joined = fork.TryJoin(out TransactionConflict? conflict);

        Assert.IsFalse(joined);
        Assert.IsNotNull(conflict);
        Assert.AreEqual("value", conflict.LogicalKey);
        Assert.IsFalse(root.GetParticipantState(participant).ContainsKey("value"));
    }

    [TestMethod]
    public void TryJoin_OwnerContinuationAndChildUseSameConflictSemantics()
    {
        DictionaryParticipant participant = new();
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction child = fork.CreateChild();
        fork.Continuation.GetParticipantState(participant).Set("value", 1);
        child.GetParticipantState(participant).Set("value", 2);
        fork.Continuation.Complete();
        child.Complete();

        bool joined = fork.TryJoin(out TransactionConflict? conflict);

        Assert.IsFalse(joined);
        Assert.IsNotNull(conflict);
        Assert.AreEqual(2, conflict.ContributorIndices.Length);
        Assert.AreEqual(0, conflict.ContributorIndices[0]);
        Assert.AreEqual(1, conflict.ContributorIndices[1]);
        Assert.IsFalse(root.GetParticipantState(participant).ContainsKey("value"));
    }

    [TestMethod]
    public void TryJoin_CustomConflictStrategyCanSelectContributor()
    {
        DictionaryParticipant participant = new();
        TransactionCoordinator coordinator = new([participant], new SelectLastContributorConflictStrategy());
        ExecutionTransaction root = coordinator.CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction first = fork.CreateChild();
        ExecutionTransaction second = fork.CreateChild();
        first.GetParticipantState(participant).Set("value", 1);
        second.GetParticipantState(participant).Set("value", 2);
        fork.Continuation.Complete();
        first.Complete();
        second.Complete();

        bool joined = fork.TryJoin(out TransactionConflict? conflict);

        Assert.IsTrue(joined, conflict?.LogicalKey.ToString());
        Assert.AreEqual(2, root.GetParticipantState(participant)["value"]);
    }

    [TestMethod]
    public void TryJoin_ConflictInLaterParticipantDoesNotPublishEarlierCandidate()
    {
        DictionaryParticipant firstParticipant = new();
        DictionaryParticipant conflictingParticipant = new();
        ExecutionTransaction root = new TransactionCoordinator([firstParticipant, conflictingParticipant]).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction first = fork.CreateChild();
        ExecutionTransaction second = fork.CreateChild();
        first.GetParticipantState(firstParticipant).Set("valid", 1);
        first.GetParticipantState(conflictingParticipant).Set("conflict", 1);
        second.GetParticipantState(conflictingParticipant).Set("conflict", 2);
        fork.Continuation.Complete();
        first.Complete();
        second.Complete();

        bool joined = fork.TryJoin(out TransactionConflict? conflict);

        Assert.IsFalse(joined);
        Assert.IsNotNull(conflict);
        Assert.AreSame(conflictingParticipant, conflict.Participant);
        Assert.IsFalse(root.GetParticipantState(firstParticipant).ContainsKey("valid"));
        Assert.IsFalse(root.GetParticipantState(conflictingParticipant).ContainsKey("conflict"));
    }

    [TestMethod]
    public void TryJoin_ParticipantPreparationExceptionLeavesOwnerUnchangedAndClosesFork()
    {
        DictionaryParticipant firstParticipant = new();
        DictionaryParticipant failingParticipant = new(failPreparation: true);
        ExecutionTransaction root = new TransactionCoordinator([firstParticipant, failingParticipant]).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction child = fork.CreateChild();
        child.GetParticipantState(firstParticipant).Set("valid", 1);
        fork.Continuation.Complete();
        child.Complete();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            fork.TryJoin(out TransactionConflict? _));

        StringAssert.Contains(exception.Message, "Synthetic preparation failure");
        Assert.AreEqual(ExecutionTransactionForkLifecycle.Failed, fork.Lifecycle);
        Assert.IsFalse(root.GetParticipantState(firstParticipant).ContainsKey("valid"));
        Assert.AreEqual(ExecutionTransactionLifecycle.Discarded, child.Lifecycle);
    }

    [TestMethod]
    public void TryJoin_RepeatedNestedForkGenerationsRetainChangesWhenJoiningUpward()
    {
        DictionaryParticipant participant = new();
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ExecutionTransactionForkGroup outerFork = root.Fork();
        ExecutionTransaction parent = outerFork.CreateChild();
        outerFork.Continuation.Complete();
        parent.GetParticipantState(participant).Set("parent", 1);

        ExecutionTransactionForkGroup firstFork = parent.Fork();
        ExecutionTransaction firstChild = firstFork.CreateChild();
        firstFork.Continuation.Complete();
        firstChild.GetParticipantState(participant).Set("first", 2);
        firstChild.Complete();
        Assert.IsTrue(firstFork.TryJoin(out TransactionConflict? firstConflict), firstConflict?.LogicalKey.ToString());

        ExecutionTransactionForkGroup secondFork = parent.Fork();
        ExecutionTransaction secondChild = secondFork.CreateChild();
        secondFork.Continuation.Complete();
        secondChild.GetParticipantState(participant).Set("second", 3);
        secondChild.Complete();
        Assert.IsTrue(secondFork.TryJoin(out TransactionConflict? secondConflict), secondConflict?.LogicalKey.ToString());

        DictionaryParticipantState parentState = parent.GetParticipantState(participant);
        Assert.AreEqual(3, parentState.ChangeCount);
        parent.Complete();
        Assert.IsTrue(outerFork.TryJoin(out TransactionConflict? outerConflict), outerConflict?.LogicalKey.ToString());

        DictionaryParticipantState rootState = root.GetParticipantState(participant);
        Assert.AreEqual(1, rootState["parent"]);
        Assert.AreEqual(2, rootState["first"]);
        Assert.AreEqual(3, rootState["second"]);
        Assert.AreEqual(3, rootState.ChangeCount);
    }

    [TestMethod]
    public void TryJoin_RequiresEveryContributorToComplete()
    {
        DictionaryParticipant participant = new();
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction child = fork.CreateChild();
        fork.Continuation.Complete();

        Assert.ThrowsExactly<InvalidOperationException>(() => fork.TryJoin(out TransactionConflict? _));
        Assert.AreEqual(ExecutionTransactionLifecycle.Active, child.Lifecycle);
        Assert.AreEqual(ExecutionTransactionForkLifecycle.Active, fork.Lifecycle);
    }

    [TestMethod]
    public void TryJoin_CompletedBranchesAndForkCannotBeReused()
    {
        DictionaryParticipant participant = new();
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction child = fork.CreateChild();
        fork.Continuation.Complete();
        child.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict), conflict?.LogicalKey.ToString());

        Assert.ThrowsExactly<InvalidOperationException>(() => child.GetParticipantState(participant));
        Assert.ThrowsExactly<InvalidOperationException>(child.Complete);
        Assert.ThrowsExactly<InvalidOperationException>(() => fork.TryJoin(out TransactionConflict? _));
        Assert.ThrowsExactly<InvalidOperationException>(fork.Discard);
    }

    [TestMethod]
    public void Discard_ClosesForkWithoutPublishingContributorState()
    {
        DictionaryParticipant participant = new();
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction child = fork.CreateChild();
        child.GetParticipantState(participant).Set("discarded", 1);

        fork.Discard();

        Assert.AreEqual(ExecutionTransactionForkLifecycle.Discarded, fork.Lifecycle);
        Assert.AreEqual(ExecutionTransactionLifecycle.Discarded, fork.Continuation.Lifecycle);
        Assert.AreEqual(ExecutionTransactionLifecycle.Discarded, child.Lifecycle);
        Assert.IsFalse(root.GetParticipantState(participant).ContainsKey("discarded"));
    }

    [TestMethod]
    public void Discard_DiscardedChildCanStillBeClosedByOwningFork()
    {
        DictionaryParticipant participant = new();
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction child = fork.CreateChild();
        child.GetParticipantState(participant).Set("discarded", 1);
        child.Discard();

        fork.Discard();

        Assert.AreEqual(ExecutionTransactionForkLifecycle.Discarded, fork.Lifecycle);
        Assert.AreEqual(ExecutionTransactionLifecycle.Discarded, child.Lifecycle);
        Assert.IsFalse(root.GetParticipantState(participant).ContainsKey("discarded"));
    }

    [TestMethod]
    public void Complete_TransactionWithOpenNestedForkIsRejected()
    {
        DictionaryParticipant participant = new();
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ExecutionTransactionForkGroup outerFork = root.Fork();
        ExecutionTransaction child = outerFork.CreateChild();
        child.Fork();

        Assert.ThrowsExactly<InvalidOperationException>(child.Complete);
        Assert.ThrowsExactly<InvalidOperationException>(outerFork.Discard);
        Assert.AreEqual(ExecutionTransactionForkLifecycle.Active, outerFork.Lifecycle);
        Assert.AreEqual(ExecutionTransactionLifecycle.Active, outerFork.Continuation.Lifecycle);
        Assert.AreEqual(ExecutionTransactionLifecycle.Active, child.Lifecycle);
    }

    [TestMethod]
    public void Coordinator_DuplicateParticipantDescriptorIsRejected()
    {
        DictionaryParticipant participant = new();

        Assert.ThrowsExactly<ArgumentException>(() => new TransactionCoordinator([participant, participant]));
    }

    private sealed class DictionaryParticipant : ITransactionParticipant<DictionaryParticipantState>
    {
        private readonly bool _failPreparation;
        private readonly KeyValuePair<string, int>[] _seed;

        public DictionaryParticipant(params (string Key, int Value)[] seed)
            : this(failPreparation: false, seed)
        {
        }

        public DictionaryParticipant(bool failPreparation, params (string Key, int Value)[] seed)
        {
            _failPreparation = failPreparation;
            _seed = seed
                .Select(static value => new KeyValuePair<string, int>(value.Key, value.Value))
                .ToArray();
        }

        public DictionaryParticipantState CreateRootState(TransactionRootSeed seed)
        {
            ArgumentNullException.ThrowIfNull(seed);
            KeyValuePair<string, int>[] values = _seed;
            if (seed.TryGet<KeyValuePair<string, int>[]>(this, out KeyValuePair<string, int>[] seededValues))
            {
                values = seededValues;
            }
            return new DictionaryParticipantState(
                new TransactionalDictionary<string, int>(values, StringComparer.Ordinal),
                _failPreparation);
        }
    }

    private sealed class DictionaryParticipantState : ITransactionParticipantState
    {
        private readonly bool _failPreparation;
        private readonly TransactionalDictionary<string, int> _values;

        public DictionaryParticipantState(TransactionalDictionary<string, int> values, bool failPreparation = false)
        {
            _values = values;
            _failPreparation = failPreparation;
        }

        public int ChangeCount => _values.ChangeCount;

        public TransactionalDictionarySnapshot<string, int> Baseline => _values.Baseline;

        public int this[string key] => _values[key];

        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public void Set(string key, int value) => _values.Set(key, value);

        public bool TryRemove(string key) => _values.TryRemove(key);

        public ITransactionParticipantFork CreateFork() =>
            new DictionaryParticipantFork(this, _failPreparation);

        internal TransactionalDictionary<string, int> Values => _values;
    }

    private sealed class DictionaryParticipantFork : ITransactionParticipantFork
    {
        private readonly bool _failPreparation;
        private readonly TransactionalDictionaryFork<string, int> _values;

        public DictionaryParticipantFork(DictionaryParticipantState owner, bool failPreparation)
        {
            _failPreparation = failPreparation;
            _values = new TransactionalDictionaryFork<string, int>(owner.Values);
        }

        public ITransactionParticipantState CreateBranch() =>
            new DictionaryParticipantState(_values.CreateBranch(), _failPreparation);

        public bool TryPrepareMerge(
            ITransactionParticipant participant,
            IReadOnlyList<ITransactionParticipantState> contributors,
            ITransactionConflictStrategy conflictStrategy,
            [NotNullWhen(true)] out ITransactionParticipantState? candidate,
            out TransactionConflict? conflict)
        {
            if (_failPreparation)
            {
                throw new InvalidOperationException("Synthetic preparation failure.");
            }

            TransactionalDictionary<string, int>[] contributorValues = new TransactionalDictionary<string, int>[contributors.Count];
            for (int i = 0; i < contributors.Count; i++)
            {
                contributorValues[i] = ((DictionaryParticipantState)contributors[i]).Values;
            }

            if (!_values.TrySelectChanges(
                participant,
                contributorValues,
                static key => key,
                conflictStrategy,
                out Dictionary<string, TransactionalDictionaryChange<int>>? selectedChanges,
                out conflict))
            {
                candidate = null;
                return false;
            }

            candidate = new DictionaryParticipantState(_values.PrepareCandidate(selectedChanges), _failPreparation);
            conflict = null;
            return true;
        }
    }

    private sealed class SelectLastContributorConflictStrategy : ITransactionConflictStrategy
    {
        public TransactionConflictResolution Resolve(TransactionConflict conflict)
        {
            ArgumentNullException.ThrowIfNull(conflict);
            return TransactionConflictResolution.UseContributor(conflict.ContributorIndices[^1]);
        }
    }
}
