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
            ((ITransactionBoundTransactionalServiceContext)context).Bind(Services, transaction);
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
