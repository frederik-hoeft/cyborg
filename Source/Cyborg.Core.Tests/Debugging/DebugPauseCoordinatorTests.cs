using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Hooks;
using Cyborg.Core.Runtime.Services.Debugging;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class DebugPauseCoordinatorTests
{
    [TestMethod]
    public async Task AcquireAsync_QueuesFifoAndProjectsPausedVersusCurrentState()
    {
        RecordingTopology topology = new();
        DebugSessionState session = new();
        DebugPauseCoordinator coordinator = new(topology, session);
        ModuleExecutionId firstId = ModuleExecutionId.Create();
        ModuleExecutionId secondId = ModuleExecutionId.Create();
        ModuleExecutionId thirdId = ModuleExecutionId.Create();

        DebugPauseLease? first = await coordinator.AcquireAsync(firstId, session.Generation, CancellationToken.None);
        ValueTask<DebugPauseLease?> secondTask = coordinator.AcquireAsync(secondId, session.Generation, CancellationToken.None);
        ValueTask<DebugPauseLease?> thirdTask = coordinator.AcquireAsync(thirdId, session.Generation, CancellationToken.None);

        Assert.IsNotNull(first);
        Assert.IsFalse(secondTask.IsCompleted);
        Assert.IsFalse(thirdTask.IsCompleted);
        Assert.AreEqual(ExecutionTreeNodeState.Current, topology.GetState(firstId));
        Assert.AreEqual(ExecutionTreeNodeState.Paused, topology.GetState(secondId));
        Assert.AreEqual(ExecutionTreeNodeState.Paused, topology.GetState(thirdId));

        first.Dispose();
        DebugPauseLease? second = await secondTask;
        Assert.IsNotNull(second);
        Assert.AreEqual(ExecutionTreeNodeState.Running, topology.GetState(firstId));
        Assert.AreEqual(ExecutionTreeNodeState.Current, topology.GetState(secondId));
        Assert.AreEqual(ExecutionTreeNodeState.Paused, topology.GetState(thirdId));

        second.Dispose();
        DebugPauseLease? third = await thirdTask;
        Assert.IsNotNull(third);
        Assert.AreEqual(ExecutionTreeNodeState.Running, topology.GetState(secondId));
        Assert.AreEqual(ExecutionTreeNodeState.Current, topology.GetState(thirdId));
        third.Dispose();
        Assert.AreEqual(ExecutionTreeNodeState.Running, topology.GetState(thirdId));
    }

    [TestMethod]
    public async Task AcquireAsync_QueuedCancellationRemovesRequestAndRestoresRunningState()
    {
        RecordingTopology topology = new();
        DebugSessionState session = new();
        DebugPauseCoordinator coordinator = new(topology, session);
        ModuleExecutionId firstId = ModuleExecutionId.Create();
        ModuleExecutionId canceledId = ModuleExecutionId.Create();
        using CancellationTokenSource cancellation = new();

        DebugPauseLease? first = await coordinator.AcquireAsync(firstId, session.Generation, CancellationToken.None);
        ValueTask<DebugPauseLease?> canceledTask = coordinator.AcquireAsync(canceledId, session.Generation, cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await canceledTask);
        Assert.AreEqual(ExecutionTreeNodeState.Running, topology.GetState(canceledId));
        first!.Dispose();
    }

    [TestMethod]
    public async Task AcquireAsync_SessionInvalidationSuppressesQueuedPause()
    {
        RecordingTopology topology = new();
        DebugSessionState session = new();
        DebugPauseCoordinator coordinator = new(topology, session);
        ModuleExecutionId firstId = ModuleExecutionId.Create();
        ModuleExecutionId staleId = ModuleExecutionId.Create();
        long generation = session.Generation;

        DebugPauseLease? first = await coordinator.AcquireAsync(firstId, generation, CancellationToken.None);
        ValueTask<DebugPauseLease?> staleTask = coordinator.AcquireAsync(staleId, generation, CancellationToken.None);
        session.Invalidate();
        first!.Dispose();

        DebugPauseLease? stale = await staleTask;
        Assert.IsNull(stale);
        Assert.AreEqual(ExecutionTreeNodeState.Running, topology.GetState(staleId));
    }

    private sealed class RecordingTopology : IDebugExecutionTopologyController
    {
        private readonly object _lock = new();
        private readonly Dictionary<ModuleExecutionId, ExecutionTreeNodeState> _states = [];

        public int Priority => 0;

        public ExecutionTreeNodeState GetState(ModuleExecutionId executionId)
        {
            lock (_lock)
            {
                return _states[executionId];
            }
        }

        public bool MarkPaused(ModuleExecutionId executionId) => SetState(executionId, ExecutionTreeNodeState.Paused);

        public bool MarkCurrent(ModuleExecutionId executionId) => SetState(executionId, ExecutionTreeNodeState.Current);

        public bool MarkRunning(ModuleExecutionId executionId) => SetState(executionId, ExecutionTreeNodeState.Running);

        public void EnrichPreparedModule(ModuleExecutionId executionId, IModule module)
        {
        }

        public IExecutionTreeSnapshot CaptureTree() => ExecutionTreeSnapshot.Empty;

        public IReadOnlyList<IExecutionTreeNode> CaptureAncestry(ModuleExecutionId executionId) => [];

        public ValueTask OnStartedAsync(IModuleExecutionStartedContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnCompletedAsync(IModuleExecutionCompletedContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnClosedAsync(IModuleExecutionClosedContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        private bool SetState(ModuleExecutionId executionId, ExecutionTreeNodeState state)
        {
            lock (_lock)
            {
                _states[executionId] = state;
                return true;
            }
        }
    }
}
