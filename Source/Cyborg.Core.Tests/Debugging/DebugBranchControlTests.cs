using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Runtime.Services.Transactions;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class DebugBranchControlTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task DebugServices_RegisterTransactionalBranchControlAsync() => TestWithDIAsync(services =>
    {
        IDebugBranchControl control = services.GetRequiredService<IDebugBranchControl>();
        IDebugSessionState session = services.GetRequiredService<IDebugSessionState>();
        TransactionalServiceParticipant[] participants = [.. services.GetServices<TransactionalServiceParticipant>()];

        Assert.IsNotNull(control);
        Assert.IsInstanceOfType<IDebugSessionStateController>(session);
        Assert.Contains(static participant => participant is DebugBranchControlParticipant, participants);
    });

    [TestMethod]
    public void SequentialChild_InheritsStepAndContinueClearsOwnerAfterJoin()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Step();
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction child = fork.CreateChild();
        fork.Continuation.Complete();
        IDebugBranchControl childControl = harness.CreateControl(child);

        Assert.IsTrue(childControl.IsStepping);
        childControl.Continue();
        child.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));

        Assert.IsNull(conflict);
        Assert.IsFalse(rootControl.IsStepping);
    }

    [TestMethod]
    public void SequentialJoin_ControlsNextChildInheritance()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Step();

        ModuleTransactionForkGroup firstFork = harness.Root.Fork();
        ModuleTransaction firstChild = firstFork.CreateChild();
        firstFork.Continuation.Complete();
        harness.CreateControl(firstChild).Continue();
        firstChild.Complete();
        Assert.IsTrue(firstFork.TryJoin(out TransactionConflict? firstConflict));
        Assert.IsNull(firstConflict);

        ModuleTransactionForkGroup secondFork = harness.Root.Fork();
        ModuleTransaction secondChild = secondFork.CreateChild();
        secondFork.Continuation.Complete();
        IDebugBranchControl secondControl = harness.CreateControl(secondChild);

        Assert.IsFalse(secondControl.IsStepping);

        secondChild.Complete();
        Assert.IsTrue(secondFork.TryJoin(out TransactionConflict? secondConflict));
        Assert.IsNull(secondConflict);
    }

    [TestMethod]
    public void ParallelChildren_MutateStepStateIndependently()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Step();
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        fork.Continuation.Complete();
        IDebugBranchControl firstControl = harness.CreateControl(first);
        IDebugBranchControl secondControl = harness.CreateControl(second);

        firstControl.Continue();

        Assert.IsFalse(firstControl.IsStepping);
        Assert.IsTrue(secondControl.IsStepping);

        first.Complete();
        second.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNull(conflict);
        Assert.IsTrue(rootControl.IsStepping);
    }

    [TestMethod]
    public void ParallelAllChildrenContinued_ClearsParentDespiteStaleOwnerContinuation()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Step();
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        fork.Continuation.Complete();

        harness.CreateControl(first).Continue();
        harness.CreateControl(second).Continue();
        first.Complete();
        second.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));

        Assert.IsNull(conflict);
        Assert.IsFalse(rootControl.IsStepping);
    }

    [TestMethod]
    public void ParallelAnyChildStepping_RestoresParentStepState()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        fork.Continuation.Complete();

        harness.CreateControl(first).Step();
        first.Complete();
        second.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));

        Assert.IsNull(conflict);
        Assert.IsTrue(rootControl.IsStepping);
    }

    [TestMethod]
    public void SessionInvalidation_ImmediatelyInvalidatesExistingBranchStepState()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Step();
        long originalGeneration = harness.Session.Generation;

        long invalidatedGeneration = harness.Session.Invalidate();

        Assert.IsGreaterThan(originalGeneration, invalidatedGeneration);
        Assert.IsFalse(rootControl.IsStepping);
    }

    [TestMethod]
    public void SessionInvalidation_NewGenerationContinueDominatesStaleSteppingSiblingAtJoin()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Step();
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        fork.Continuation.Complete();
        IDebugBranchControl firstControl = harness.CreateControl(first);
        IDebugBranchControl secondControl = harness.CreateControl(second);
        long invalidatedGeneration = harness.Session.Invalidate();

        firstControl.Continue();
        Assert.IsFalse(firstControl.IsStepping);
        Assert.IsFalse(secondControl.IsStepping);
        first.Complete();
        second.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        DebugBranchControlState merged = harness.Services.GetState<DebugBranchControlParticipant, DebugBranchControlState>(harness.Root);

        Assert.IsNull(conflict);
        Assert.AreEqual(invalidatedGeneration, merged.SessionGeneration);
        Assert.IsFalse(merged.IsStepping);
        Assert.IsFalse(rootControl.IsStepping);
    }

    [TestMethod]
    public void SessionInvalidation_NewGenerationStepCanRestoreParentWithoutStaleGenerationInterference()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Step();
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        fork.Continuation.Complete();
        IDebugBranchControl firstControl = harness.CreateControl(first);
        IDebugBranchControl secondControl = harness.CreateControl(second);
        long invalidatedGeneration = harness.Session.Invalidate();

        firstControl.Step();
        Assert.IsTrue(firstControl.IsStepping);
        Assert.IsFalse(secondControl.IsStepping);
        first.Complete();
        second.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        DebugBranchControlState merged = harness.Services.GetState<DebugBranchControlParticipant, DebugBranchControlState>(harness.Root);

        Assert.IsNull(conflict);
        Assert.AreEqual(invalidatedGeneration, merged.SessionGeneration);
        Assert.IsTrue(merged.IsStepping);
        Assert.IsTrue(rootControl.IsStepping);
    }

    [TestMethod]
    public void BranchControlFork_MergeIsConflictFreeAndIgnoresOwnerContinuationWhenChildrenExist()
    {
        DebugBranchControlState owner = new(sessionGeneration: 7, isStepping: true);
        DebugBranchControlFork fork = new(owner);
        DebugBranchControlState continuation = fork.CreateBranch();
        DebugBranchControlState first = fork.CreateBranch();
        DebugBranchControlState second = fork.CreateBranch();
        first.IsStepping = false;
        second.IsStepping = false;
        ThrowingConflictResolver conflictResolver = new();

        bool merged = fork.TryPrepareMerge(
            [continuation, first, second],
            conflictResolver,
            out DebugBranchControlState? candidate);

        Assert.IsTrue(merged);
        Assert.IsNotNull(candidate);
        Assert.AreEqual(7, candidate.SessionGeneration);
        Assert.IsFalse(candidate.IsStepping);
        Assert.IsFalse(conflictResolver.WasCalled);
    }

    [TestMethod]
    public void BranchControlFork_ChangedOwnerContinuationParticipatesWhenChildrenExist()
    {
        DebugBranchControlState owner = new(sessionGeneration: 7, isStepping: false);
        DebugBranchControlFork fork = new(owner);
        DebugBranchControlState continuation = fork.CreateBranch();
        DebugBranchControlState child = fork.CreateBranch();
        continuation.IsStepping = true;
        child.IsStepping = false;

        bool merged = fork.TryPrepareMerge([continuation, child], new ThrowingConflictResolver(), out DebugBranchControlState? candidate);

        Assert.IsTrue(merged);
        Assert.IsNotNull(candidate);
        Assert.AreEqual(7, candidate.SessionGeneration);
        Assert.IsTrue(candidate.IsStepping);
    }

    [TestMethod]
    public void Next_ReplacesSteppingUntilStepOrContinue()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl control = harness.CreateControl(harness.Root);
        ModuleExecutionId anchor = new(Guid.NewGuid());
        control.Step();

        control.Next(anchor);

        Assert.IsFalse(control.IsStepping);
        Assert.AreEqual(anchor, control.StepOverAnchor);

        control.Step();
        Assert.IsTrue(control.IsStepping);
        Assert.IsNull(control.StepOverAnchor);

        control.Next(anchor);
        control.Continue();
        Assert.IsFalse(control.IsStepping);
        Assert.IsNull(control.StepOverAnchor);
    }

    [TestMethod]
    public void SequentialChild_InheritsStepOverAnchorAndNotStepping()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        ModuleExecutionId anchor = new(Guid.NewGuid());
        rootControl.Step();
        rootControl.Next(anchor);
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction child = fork.CreateChild();
        fork.Continuation.Complete();
        IDebugBranchControl childControl = harness.CreateControl(child);

        Assert.IsFalse(childControl.IsStepping);
        Assert.AreEqual(anchor, childControl.StepOverAnchor);

        child.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNull(conflict);
        Assert.IsFalse(rootControl.IsStepping);
        Assert.AreEqual(anchor, rootControl.StepOverAnchor);
    }

    [TestMethod]
    public void SequentialJoin_NextOnChildReplacesInheritedSteppingForTheNextChild()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Step();
        ModuleExecutionId anchor = new(Guid.NewGuid());

        ModuleTransactionForkGroup firstFork = harness.Root.Fork();
        ModuleTransaction firstChild = firstFork.CreateChild();
        firstFork.Continuation.Complete();
        harness.CreateControl(firstChild).Next(anchor);
        firstChild.Complete();
        Assert.IsTrue(firstFork.TryJoin(out TransactionConflict? firstConflict));
        Assert.IsNull(firstConflict);
        Assert.IsFalse(rootControl.IsStepping);
        Assert.AreEqual(anchor, rootControl.StepOverAnchor);

        ModuleTransactionForkGroup secondFork = harness.Root.Fork();
        ModuleTransaction secondChild = secondFork.CreateChild();
        secondFork.Continuation.Complete();
        IDebugBranchControl secondControl = harness.CreateControl(secondChild);

        Assert.IsFalse(secondControl.IsStepping);
        Assert.AreEqual(anchor, secondControl.StepOverAnchor);

        secondChild.Complete();
        Assert.IsTrue(secondFork.TryJoin(out TransactionConflict? secondConflict));
        Assert.IsNull(secondConflict);
    }

    [TestMethod]
    public void ParallelChildren_NextOnOneBranchDoesNotAffectTheOtherBeforeJoin()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Step();
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        fork.Continuation.Complete();
        IDebugBranchControl firstControl = harness.CreateControl(first);
        IDebugBranchControl secondControl = harness.CreateControl(second);
        ModuleExecutionId anchor = new(Guid.NewGuid());

        firstControl.Next(anchor);

        Assert.IsFalse(firstControl.IsStepping);
        Assert.AreEqual(anchor, firstControl.StepOverAnchor);
        Assert.IsTrue(secondControl.IsStepping);
        Assert.IsNull(secondControl.StepOverAnchor);

        first.Complete();
        second.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNull(conflict);
        Assert.IsFalse(rootControl.IsStepping);
        Assert.AreEqual(anchor, rootControl.StepOverAnchor);
    }

    [TestMethod]
    public void ParallelJoin_LaterNextOverridesInheritedStepOverAnchor()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        ModuleExecutionId outerAnchor = new(Guid.NewGuid());
        ModuleExecutionId innerAnchor = new(Guid.NewGuid());
        rootControl.Next(outerAnchor);
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        fork.Continuation.Complete();

        harness.CreateControl(second).Next(innerAnchor);
        first.Complete();
        second.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));

        Assert.IsNull(conflict);
        Assert.IsFalse(rootControl.IsStepping);
        Assert.AreEqual(innerAnchor, rootControl.StepOverAnchor);
    }

    [TestMethod]
    public void ParallelJoin_LaterContinueOverridesInheritedStepOverAnchor()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Next(new ModuleExecutionId(Guid.NewGuid()));
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        fork.Continuation.Complete();

        harness.CreateControl(second).Continue();
        first.Complete();
        second.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));

        Assert.IsNull(conflict);
        Assert.IsFalse(rootControl.IsStepping);
        Assert.IsNull(rootControl.StepOverAnchor);
    }

    [TestMethod]
    public void ParallelJoin_PendingNextSurvivesWhenNoChildRemainsStepping()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Step();
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        fork.Continuation.Complete();
        ModuleExecutionId anchor = new(Guid.NewGuid());

        harness.CreateControl(first).Continue();
        harness.CreateControl(second).Next(anchor);
        first.Complete();
        second.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));

        Assert.IsNull(conflict);
        Assert.IsFalse(rootControl.IsStepping);
        Assert.AreEqual(anchor, rootControl.StepOverAnchor);
    }

    [TestMethod]
    public void ParallelAllChildrenContinued_ClearsInheritedStepOverAnchor()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Next(new ModuleExecutionId(Guid.NewGuid()));
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        fork.Continuation.Complete();

        harness.CreateControl(first).Continue();
        harness.CreateControl(second).Continue();
        first.Complete();
        second.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));

        Assert.IsNull(conflict);
        Assert.IsFalse(rootControl.IsStepping);
        Assert.IsNull(rootControl.StepOverAnchor);
    }

    [TestMethod]
    public void Rollback_NextDecisionStillPublishesToOwner()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction child = fork.CreateChild();
        fork.Continuation.Complete();
        ModuleExecutionId anchor = new(Guid.NewGuid());
        harness.CreateControl(child).Next(anchor);

        child.Complete(TransactionPublicationDisposition.Rollback);
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));

        Assert.IsNull(conflict);
        Assert.IsFalse(rootControl.IsStepping);
        Assert.AreEqual(anchor, rootControl.StepOverAnchor);
    }

    [TestMethod]
    public void SessionInvalidation_HidesStepOverAnchorUntilANewCommand()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        ModuleExecutionId staleAnchor = new(Guid.NewGuid());
        rootControl.Next(staleAnchor);

        harness.Session.Invalidate();

        Assert.IsFalse(rootControl.IsStepping);
        Assert.IsNull(rootControl.StepOverAnchor);

        ModuleExecutionId freshAnchor = new(Guid.NewGuid());
        rootControl.Next(freshAnchor);
        Assert.IsFalse(rootControl.IsStepping);
        Assert.AreEqual(freshAnchor, rootControl.StepOverAnchor);
    }

    [TestMethod]
    public void SessionInvalidation_NewGenerationContinueDominatesStaleStepOverSiblingAtJoin()
    {
        DebugControlHarness harness = new();
        IDebugBranchControl rootControl = harness.CreateControl(harness.Root);
        rootControl.Next(new ModuleExecutionId(Guid.NewGuid()));
        ModuleTransactionForkGroup fork = harness.Root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        fork.Continuation.Complete();
        IDebugBranchControl firstControl = harness.CreateControl(first);
        long invalidatedGeneration = harness.Session.Invalidate();

        firstControl.Continue();
        first.Complete();
        second.Complete();
        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        DebugBranchControlState merged = harness.Services.GetState<DebugBranchControlParticipant, DebugBranchControlState>(harness.Root);

        Assert.IsNull(conflict);
        Assert.AreEqual(invalidatedGeneration, merged.SessionGeneration);
        Assert.IsFalse(merged.IsStepping);
        Assert.IsNull(merged.StepOverAnchor);
        Assert.IsNull(rootControl.StepOverAnchor);
    }

    [TestMethod]
    public void BranchControlFork_LaterContinuationCommandParticipatesWhenChildrenExist()
    {
        ModuleExecutionId originalAnchor = new(Guid.NewGuid());
        ModuleExecutionId continuationAnchor = new(Guid.NewGuid());
        DebugBranchControlState owner = new(
            sessionGeneration: 7,
            isStepping: false,
            stepOverAnchor: originalAnchor,
            controlCommandSequence: 1,
            requiresCommandOrdering: true);
        DebugBranchControlFork fork = new(owner);
        DebugBranchControlState continuation = fork.CreateBranch();
        DebugBranchControlState child = fork.CreateBranch();
        continuation.StepOverAnchor = continuationAnchor;
        continuation.ControlCommandSequence = 3;
        child.StepOverAnchor = null;
        child.ControlCommandSequence = 2;

        bool merged = fork.TryPrepareMerge([continuation, child], new ThrowingConflictResolver(), out DebugBranchControlState? candidate);

        Assert.IsTrue(merged);
        Assert.IsNotNull(candidate);
        Assert.IsFalse(candidate.IsStepping);
        Assert.AreEqual(continuationAnchor, candidate.StepOverAnchor);
    }

    [TestMethod]
    public void BranchControlFork_RepeatedContinuationCommandParticipatesBySequence()
    {
        ModuleExecutionId anchor = new(Guid.NewGuid());
        DebugBranchControlState owner = new(
            sessionGeneration: 7,
            isStepping: false,
            stepOverAnchor: anchor,
            controlCommandSequence: 1,
            requiresCommandOrdering: true);
        DebugBranchControlFork fork = new(owner);
        DebugBranchControlState continuation = fork.CreateBranch();
        DebugBranchControlState child = fork.CreateBranch();
        continuation.ControlCommandSequence = 3;
        child.StepOverAnchor = null;
        child.ControlCommandSequence = 2;

        bool merged = fork.TryPrepareMerge([continuation, child], new ThrowingConflictResolver(), out DebugBranchControlState? candidate);

        Assert.IsTrue(merged);
        Assert.IsNotNull(candidate);
        Assert.IsFalse(candidate.IsStepping);
        Assert.AreEqual(anchor, candidate.StepOverAnchor);
    }

    [TestMethod]
    public void BranchControlFork_LaterStepOverridesEarlierNext()
    {
        ModuleExecutionId originalAnchor = new(Guid.NewGuid());
        ModuleExecutionId childAnchor = new(Guid.NewGuid());
        DebugBranchControlState owner = new(
            sessionGeneration: 4,
            isStepping: false,
            stepOverAnchor: originalAnchor,
            controlCommandSequence: 1,
            requiresCommandOrdering: true);
        DebugBranchControlFork fork = new(owner);
        DebugBranchControlState continuation = fork.CreateBranch();
        DebugBranchControlState first = fork.CreateBranch();
        DebugBranchControlState second = fork.CreateBranch();
        first.StepOverAnchor = childAnchor;
        first.ControlCommandSequence = 2;
        second.IsStepping = true;
        second.StepOverAnchor = null;
        second.ControlCommandSequence = 3;

        bool merged = fork.TryPrepareMerge([continuation, first, second], new ThrowingConflictResolver(), out DebugBranchControlState? candidate);

        Assert.IsTrue(merged);
        Assert.IsNotNull(candidate);
        Assert.IsTrue(candidate.IsStepping);
        Assert.IsNull(candidate.StepOverAnchor);
    }

    [TestMethod]
    public void BranchControlFork_LaterNextOverridesEarlierStep()
    {
        ModuleExecutionId anchor = new(Guid.NewGuid());
        DebugBranchControlState owner = new(sessionGeneration: 4, isStepping: false);
        DebugBranchControlFork fork = new(owner);
        DebugBranchControlState continuation = fork.CreateBranch();
        DebugBranchControlState first = fork.CreateBranch();
        DebugBranchControlState second = fork.CreateBranch();
        first.IsStepping = true;
        first.ControlCommandSequence = 1;
        second.StepOverAnchor = anchor;
        second.ControlCommandSequence = 2;
        second.RequiresCommandOrdering = true;

        bool merged = fork.TryPrepareMerge([continuation, first, second], new ThrowingConflictResolver(), out DebugBranchControlState? candidate);

        Assert.IsTrue(merged);
        Assert.IsNotNull(candidate);
        Assert.IsFalse(candidate.IsStepping);
        Assert.AreEqual(anchor, candidate.StepOverAnchor);
    }

    private sealed class DebugControlHarness
    {
        public DebugControlHarness()
        {
            Session = new DebugSessionState();
            Participant = new DebugBranchControlParticipant(Session);
            Services = new RuntimeTransactionalServices([Participant]);
            Root = new TransactionCoordinator(Services.Participants).CreateRoot();
        }

        public DebugSessionState Session { get; }

        public DebugBranchControlParticipant Participant { get; }

        public RuntimeTransactionalServices Services { get; }

        public ModuleTransaction Root { get; }

        public IDebugBranchControl CreateControl(ModuleTransaction transaction)
        {
            TransactionalServiceContext context = new();
            ((ITransactionBoundTransactionalServiceContext)context).Bind(Services, new ActiveTransaction(transaction));
            return new DebugBranchControl(context, Session);
        }
    }

    private sealed class ThrowingConflictResolver : ITransactionalServiceConflictResolver
    {
        public bool WasCalled { get; private set; }

        public bool TryResolve(object logicalKey, IReadOnlyList<int> contributorIndices, out int selectedContributorIndex)
        {
            WasCalled = true;
            throw new AssertFailedException("Debugger branch-control merge must not delegate to workflow conflict resolution.");
        }
    }
}
