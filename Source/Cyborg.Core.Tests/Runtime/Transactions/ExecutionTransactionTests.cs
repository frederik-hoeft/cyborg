using Cyborg.Core.Runtime.Engine.Transactions.Collections;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Tests.Runtime.Transactions;

[TestClass]
public sealed class ExecutionTransactionTests
{
    [TestMethod]
    public void CreateRoot_MultipleRootsHaveIndependentParticipantState()
    {
        DictionaryParticipant participant = new();
        TransactionCoordinator coordinator = new([participant]);
        ModuleTransaction first = coordinator.CreateRoot();
        ModuleTransaction second = coordinator.CreateRoot();

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

        ModuleTransaction first = coordinator.CreateRoot(firstSeed);
        ModuleTransaction second = coordinator.CreateRoot(secondSeed);

        Assert.AreEqual(1, first.GetParticipantState(participant)["seed"]);
        Assert.AreEqual(2, second.GetParticipantState(participant)["seed"]);
    }

    [TestMethod]
    public void Fork_ContinuationAndChildrenShareStableBaselineAndRemainIsolated()
    {
        DictionaryParticipant participant = new(("baseline", 1));
        TransactionCoordinator coordinator = new([participant]);
        ModuleTransaction root = coordinator.CreateRoot();
        root.GetParticipantState(participant).Set("before-fork", 2);

        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
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
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot();

        ModuleTransactionForkGroup fork = root.Fork();

        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetParticipantState(participant));
        Assert.ThrowsExactly<InvalidOperationException>(root.Fork);
        Assert.ThrowsExactly<InvalidOperationException>(root.Complete);
        fork.Discard();
        Assert.AreEqual(ModuleTransactionLifecycle.Active, root.Lifecycle);
        Assert.IsNotNull(root.GetParticipantState(participant));
    }

    [TestMethod]
    public void TryJoin_NonOverlappingContinuationAndChildChangesPublishAtomically()
    {
        DictionaryParticipant participant = new();
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction child = fork.CreateChild();
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
        Assert.AreEqual(ModuleTransactionForkLifecycle.Joined, fork.Lifecycle);
        Assert.AreEqual(ModuleTransactionLifecycle.Joined, fork.Continuation.Lifecycle);
        Assert.AreEqual(ModuleTransactionLifecycle.Joined, child.Lifecycle);
    }

    [TestMethod]
    public void TryJoin_SiblingWriteConflictLeavesOwnerUnchanged()
    {
        DictionaryParticipant participant = new(("baseline", 1));
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
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
        Assert.HasCount(2, conflict.ContributorIndices);
        Assert.AreEqual(1, conflict.ContributorIndices[0]);
        Assert.AreEqual(2, conflict.ContributorIndices[1]);
        Assert.AreEqual(1, root.GetParticipantState(participant)["baseline"]);
        Assert.IsFalse(root.GetParticipantState(participant).ContainsKey("value"));
        Assert.AreEqual(ModuleTransactionForkLifecycle.Conflict, fork.Lifecycle);
        Assert.AreEqual(ModuleTransactionLifecycle.Discarded, first.Lifecycle);
        Assert.AreEqual(ModuleTransactionLifecycle.Discarded, second.Lifecycle);
    }

    [TestMethod]
    public void TryJoin_AddThenRemoveStillParticipatesInConflictDetection()
    {
        DictionaryParticipant participant = new();
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
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
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction child = fork.CreateChild();
        fork.Continuation.GetParticipantState(participant).Set("value", 1);
        child.GetParticipantState(participant).Set("value", 2);
        fork.Continuation.Complete();
        child.Complete();

        bool joined = fork.TryJoin(out TransactionConflict? conflict);

        Assert.IsFalse(joined);
        Assert.IsNotNull(conflict);
        Assert.HasCount(2, conflict.ContributorIndices);
        Assert.AreEqual(0, conflict.ContributorIndices[0]);
        Assert.AreEqual(1, conflict.ContributorIndices[1]);
        Assert.IsFalse(root.GetParticipantState(participant).ContainsKey("value"));
    }

    [TestMethod]
    public void TryJoin_CustomConflictStrategyCanSelectContributor()
    {
        DictionaryParticipant participant = new();
        TransactionCoordinator coordinator = new([participant], new SelectLastContributorConflictStrategy());
        ModuleTransaction root = coordinator.CreateRoot();
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
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
        ModuleTransaction root = new TransactionCoordinator([firstParticipant, conflictingParticipant]).CreateRoot();
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
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
        ModuleTransaction root = new TransactionCoordinator([firstParticipant, failingParticipant]).CreateRoot();
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction child = fork.CreateChild();
        child.GetParticipantState(firstParticipant).Set("valid", 1);
        fork.Continuation.Complete();
        child.Complete();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            fork.TryJoin(out TransactionConflict? _));

        Assert.Contains("Synthetic preparation failure", exception.Message);
        Assert.AreEqual(ModuleTransactionForkLifecycle.Failed, fork.Lifecycle);
        Assert.IsFalse(root.GetParticipantState(firstParticipant).ContainsKey("valid"));
        Assert.AreEqual(ModuleTransactionLifecycle.Discarded, child.Lifecycle);
    }

    [TestMethod]
    public void TryJoin_RepeatedNestedForkGenerationsRetainChangesWhenJoiningUpward()
    {
        DictionaryParticipant participant = new();
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ModuleTransactionForkGroup outerFork = root.Fork();
        ModuleTransaction parent = outerFork.CreateChild();
        outerFork.Continuation.Complete();
        parent.GetParticipantState(participant).Set("parent", 1);

        ModuleTransactionForkGroup firstFork = parent.Fork();
        ModuleTransaction firstChild = firstFork.CreateChild();
        firstFork.Continuation.Complete();
        firstChild.GetParticipantState(participant).Set("first", 2);
        firstChild.Complete();
        Assert.IsTrue(firstFork.TryJoin(out TransactionConflict? firstConflict), firstConflict?.LogicalKey.ToString());

        ModuleTransactionForkGroup secondFork = parent.Fork();
        ModuleTransaction secondChild = secondFork.CreateChild();
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
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction child = fork.CreateChild();
        fork.Continuation.Complete();

        Assert.ThrowsExactly<InvalidOperationException>(() => fork.TryJoin(out TransactionConflict? _));
        Assert.AreEqual(ModuleTransactionLifecycle.Active, child.Lifecycle);
        Assert.AreEqual(ModuleTransactionForkLifecycle.Active, fork.Lifecycle);
    }

    [TestMethod]
    public void TryJoin_CompletedBranchesAndForkCannotBeReused()
    {
        DictionaryParticipant participant = new();
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction child = fork.CreateChild();
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
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction child = fork.CreateChild();
        child.GetParticipantState(participant).Set("discarded", 1);

        fork.Discard();

        Assert.AreEqual(ModuleTransactionForkLifecycle.Discarded, fork.Lifecycle);
        Assert.AreEqual(ModuleTransactionLifecycle.Discarded, fork.Continuation.Lifecycle);
        Assert.AreEqual(ModuleTransactionLifecycle.Discarded, child.Lifecycle);
        Assert.IsFalse(root.GetParticipantState(participant).ContainsKey("discarded"));
    }

    [TestMethod]
    public void Discard_DiscardedChildCanStillBeClosedByOwningFork()
    {
        DictionaryParticipant participant = new();
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction child = fork.CreateChild();
        child.GetParticipantState(participant).Set("discarded", 1);
        child.Discard();

        fork.Discard();

        Assert.AreEqual(ModuleTransactionForkLifecycle.Discarded, fork.Lifecycle);
        Assert.AreEqual(ModuleTransactionLifecycle.Discarded, child.Lifecycle);
        Assert.IsFalse(root.GetParticipantState(participant).ContainsKey("discarded"));
    }

    [TestMethod]
    public void Complete_TransactionWithOpenNestedForkIsRejected()
    {
        DictionaryParticipant participant = new();
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot();
        ModuleTransactionForkGroup outerFork = root.Fork();
        ModuleTransaction child = outerFork.CreateChild();
        child.Fork();

        Assert.ThrowsExactly<InvalidOperationException>(child.Complete);
        Assert.ThrowsExactly<InvalidOperationException>(outerFork.Discard);
        Assert.AreEqual(ModuleTransactionForkLifecycle.Active, outerFork.Lifecycle);
        Assert.AreEqual(ModuleTransactionLifecycle.Active, outerFork.Continuation.Lifecycle);
        Assert.AreEqual(ModuleTransactionLifecycle.Active, child.Lifecycle);
    }

    [TestMethod]
    public void Coordinator_DuplicateParticipantDescriptorIsRejected()
    {
        DictionaryParticipant participant = new();

        Assert.ThrowsExactly<ArgumentException>(() => new TransactionCoordinator([participant, participant]));
    }

    private sealed class DictionaryParticipant(bool failPreparation, params (string Key, int Value)[] seed) : ITransactionParticipant<DictionaryParticipantState>
    {
        private readonly KeyValuePair<string, int>[] _seed = [.. seed.Select(static value => KeyValuePair.Create(value.Key, value.Value))];

        public DictionaryParticipant(params (string Key, int Value)[] seed) : this(failPreparation: false, seed)
        {
        }

        public DictionaryParticipantState CreateRootState(TransactionRootSeed seed)
        {
            ArgumentNullException.ThrowIfNull(seed);
            KeyValuePair<string, int>[] values = _seed;
            if (seed.TryGet(this, out KeyValuePair<string, int>[]? seededValues))
            {
                values = seededValues;
            }
            return new DictionaryParticipantState(values.ToTransactionalDictionary(StringComparer.Ordinal), failPreparation);
        }
    }

    private sealed class DictionaryParticipantState(TransactionalDictionary<string, int> values, bool failPreparation = false) : ITransactionParticipantState
    {
        public int ChangeCount => values.ChangeCount;

        public TransactionalDictionarySnapshot<string, int> Baseline => values.Baseline;

        public int this[string key] => values[key];

        public bool ContainsKey(string key) => values.ContainsKey(key);

        public void Set(string key, int value) => values.Set(key, value);

        public bool TryRemove(string key) => values.TryRemove(key);

        public ITransactionParticipantFork CreateFork() =>
            new DictionaryParticipantFork(this, failPreparation);

        internal TransactionalDictionary<string, int> Values => values;
    }

    private sealed class DictionaryParticipantFork(ExecutionTransactionTests.DictionaryParticipantState owner, bool failPreparation) : ITransactionParticipantFork
    {
        private readonly TransactionalDictionaryFork<string, int> _values = new(owner.Values);

        public ITransactionParticipantState CreateBranch() => new DictionaryParticipantState(_values.CreateBranch(), failPreparation);

        public bool TryPrepareMerge(
            ITransactionParticipant participant,
            IReadOnlyList<ITransactionParticipantState> contributors,
            ITransactionConflictStrategy conflictStrategy,
            [NotNullWhen(true)] out ITransactionParticipantState? candidate,
            out TransactionConflict? conflict)
        {
            if (failPreparation)
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

            candidate = new DictionaryParticipantState(_values.PrepareCandidate(selectedChanges), failPreparation);
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
