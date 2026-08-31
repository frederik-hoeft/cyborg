using System.Diagnostics.CodeAnalysis;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Hooks;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Runtime.Services.Validation;
using Cyborg.Core.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class DebugExecutionTopologyTests : CyborgCoreTestBase
{
    [TestMethod]
    public async Task Test_Topology_PausedBranchAndRunningSiblingShareStableTreeAndStackAsync()
    {
        DebugExecutionTopology topology = new();
        ModuleExecutionId rootId = CreateExecutionId();
        ModuleExecutionId pausedId = CreateExecutionId();
        ModuleExecutionId validatingId = CreateExecutionId();

        await StartAsync(topology, rootId, parentExecutionId: null, "root");
        await StartAsync(topology, pausedId, rootId, "paused");
        await StartAsync(topology, validatingId, rootId, "validating");
        Assert.IsTrue(topology.MarkPaused(pausedId));
        Assert.IsTrue(topology.MarkCurrent(pausedId));

        IExecutionTreeSnapshot snapshot = topology.CaptureTree();
        IExecutionTreeNode root = AssertSingle(snapshot.Roots);
        Assert.AreEqual(rootId, root.ExecutionId);
        Assert.HasCount(2, root.Children);
        IExecutionTreeNode paused = AssertSingle(root.Children.Where(node => node.ExecutionId == pausedId).ToArray());
        IExecutionTreeNode validating = AssertSingle(root.Children.Where(node => node.ExecutionId == validatingId).ToArray());
        Assert.AreEqual(ExecutionTreeNodeState.Current, paused.State);
        Assert.AreEqual(ExecutionTreeNodeState.Running, validating.State);

        IReadOnlyList<IExecutionTreeNode> stack = topology.CaptureAncestry(pausedId);
        Assert.HasCount(2, stack);
        Assert.AreEqual(pausedId, stack[0].ExecutionId);
        Assert.AreEqual(rootId, stack[1].ExecutionId);
        Assert.HasCount(0, stack[0].Children);
        Assert.HasCount(0, stack[1].Children);
    }

    [TestMethod]
    public async Task Test_Topology_PreparedMetadataEnrichesLiveNodeWithoutMutatingExistingSnapshotAsync()
    {
        DebugExecutionTopology topology = new();
        ModuleExecutionId executionId = CreateExecutionId();
        TestModule initial = new() { Name = "before", Group = "initial" };
        TestModule prepared = new() { Name = "after", Group = "prepared" };
        await StartAsync(topology, executionId, parentExecutionId: null, initial);

        IExecutionTreeSnapshot before = topology.CaptureTree();
        topology.EnrichPreparedModule(executionId, prepared);
        IExecutionTreeSnapshot after = topology.CaptureTree();

        Assert.AreEqual("before", AssertSingle(before.Roots).Name);
        Assert.AreEqual("initial", AssertSingle(before.Roots).Group);
        Assert.AreEqual("after", AssertSingle(after.Roots).Name);
        Assert.AreEqual("prepared", AssertSingle(after.Roots).Group);
    }

    [TestMethod]
    public async Task Test_Topology_CompletedNodeRetainsOutcomeUntilClosedThenPrunesAsync()
    {
        DebugExecutionTopology topology = new();
        ModuleExecutionId executionId = CreateExecutionId();
        TestModule module = new() { Name = "leaf" };
        TestLifecycleContext context = CreateContext(executionId, parentExecutionId: null, module);
        await topology.OnStartedAsync(context, CancellationToken.None);

        context.Result = new TestExecutionResult(module, ModuleExitStatus.Failed);
        await topology.OnCompletedAsync(context, CancellationToken.None);

        IExecutionTreeNode completed = AssertSingle(topology.CaptureTree().Roots);
        Assert.AreEqual(ExecutionTreeNodeState.Completed, completed.State);
        Assert.AreEqual(ModuleExitStatus.Failed, completed.ExitStatus);

        context.Joined = true;
        await topology.OnClosedAsync(context, CancellationToken.None);

        Assert.HasCount(0, topology.CaptureTree().Roots);
        Assert.HasCount(0, topology.CaptureAncestry(executionId));
    }

    [TestMethod]
    public async Task Test_Topology_PauseTransitionsRejectCompletedAndInvalidTransitionsAsync()
    {
        DebugExecutionTopology topology = new();
        ModuleExecutionId executionId = CreateExecutionId();
        TestModule module = new() { Name = "leaf" };
        TestLifecycleContext context = CreateContext(executionId, parentExecutionId: null, module);
        await topology.OnStartedAsync(context, CancellationToken.None);

        Assert.IsFalse(topology.MarkCurrent(executionId));
        Assert.IsTrue(topology.MarkPaused(executionId));
        Assert.IsFalse(topology.MarkPaused(executionId));
        Assert.IsTrue(topology.MarkCurrent(executionId));
        Assert.IsTrue(topology.MarkRunning(executionId));
        Assert.IsFalse(topology.MarkRunning(executionId));

        context.Result = new TestExecutionResult(module, ModuleExitStatus.Success);
        await topology.OnCompletedAsync(context, CancellationToken.None);
        Assert.IsFalse(topology.MarkPaused(executionId));
        Assert.IsFalse(topology.MarkCurrent(executionId));
        Assert.IsFalse(topology.MarkRunning(executionId));
    }


    [TestMethod]
    public Task Test_Topology_DebuggingHookEnrichesPreparedMetadataBeforeLaterPreExecutionHooksAsync() => TestWithDIAsync(async services =>
    {
        PreparedMetadataObservation observation = services.GetRequiredService<PreparedMetadataObservation>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        PreparedTopologyProbeModule module = new()
        {
            Name = "before",
            Group = "initial",
            Artifacts = PreparedArtifacts,
        };

        IModuleExecutionResult result = await runtime.ExecuteAsync(
            new ModuleReference(module, PreparedTopologyProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.AreEqual("after", observation.Name);
        Assert.AreEqual("prepared", observation.Group);
    }, ConfigurePreparedMetadataServices);

    [TestMethod]
    public Task Test_Topology_RuntimeKeepsCompletedParallelSiblingUntilJoinThenPrunesAsync() => TestWithDIAsync(async services =>
    {
        IDebugExecutionTopology topology = services.GetRequiredService<IDebugExecutionTopology>();
        TopologyProbeLifecycleHook lifecycle = services.GetRequiredService<TopologyProbeLifecycleHook>();
        TopologyProbeState state = services.GetRequiredService<TopologyProbeState>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        Task<IReadOnlyList<IModuleExecutionResult>> execution = runtime.ExecuteConcurrentlyAsync(
            [
                CreateProbeContext("fast", TopologyProbeBehavior.WaitForSlowStart),
                CreateProbeContext("slow", TopologyProbeBehavior.WaitForRelease),
            ],
            TestContext.CancellationToken);

        await lifecycle.FastCompleted.Task.WaitAsync(TestContext.CancellationToken);

        IExecutionTreeSnapshot openSnapshot = topology.CaptureTree();
        Assert.HasCount(2, openSnapshot.Roots);
        IExecutionTreeNode fast = FindByName(openSnapshot, "fast");
        IExecutionTreeNode slow = FindByName(openSnapshot, "slow");
        Assert.AreEqual(ExecutionTreeNodeState.Completed, fast.State);
        Assert.AreEqual(ModuleExitStatus.Success, fast.ExitStatus);
        Assert.AreEqual(ExecutionTreeNodeState.Running, slow.State);

        state.ReleaseSlow.TrySetResult();
        IReadOnlyList<IModuleExecutionResult> results = await execution;

        Assert.HasCount(2, results);
        Assert.IsTrue(results.All(static result => result.Status == ModuleExitStatus.Success));
        Assert.HasCount(0, topology.CaptureTree().Roots);
    }, ConfigureTopologyProbeServices);

    [TestMethod]
    public Task Test_Topology_RuntimeShowsSiblingThatFailsBeforePreExecutionAndPrunesOnDiscardAsync() => TestWithDIAsync(async services =>
    {
        IDebugExecutionTopology topology = services.GetRequiredService<IDebugExecutionTopology>();
        TopologyProbeLifecycleHook lifecycle = services.GetRequiredService<TopologyProbeLifecycleHook>();
        TopologyProbeState state = services.GetRequiredService<TopologyProbeState>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        Task<IReadOnlyList<IModuleExecutionResult>> execution = runtime.ExecuteConcurrentlyAsync(
            [
                CreateProbeContext("activation-failure", TopologyProbeBehavior.ActivationFailure),
                CreateProbeContext("slow", TopologyProbeBehavior.WaitForRelease),
            ],
            TestContext.CancellationToken);

        await lifecycle.ActivationFailureStarted.Task.WaitAsync(TestContext.CancellationToken);
        await state.SlowStarted.Task.WaitAsync(TestContext.CancellationToken);

        IExecutionTreeSnapshot openSnapshot = topology.CaptureTree();
        Assert.HasCount(2, openSnapshot.Roots);
        IExecutionTreeNode failedBeforePreparation = FindByName(openSnapshot, "activation-failure");
        IExecutionTreeNode slow = FindByName(openSnapshot, "slow");
        Assert.AreEqual(ExecutionTreeNodeState.Running, failedBeforePreparation.State);
        Assert.AreEqual(ExecutionTreeNodeState.Running, slow.State);

        state.ReleaseSlow.TrySetResult();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await execution);

        Assert.HasCount(0, topology.CaptureTree().Roots);
    }, ConfigureTopologyProbeServices);


    private static void ConfigurePreparedMetadataServices(IServiceCollection services)
    {
        services.RemoveAll<IModuleWorkerFactory>();
        services.AddSingleton<PreparedMetadataObservation>();
        services.AddSingleton<IModulePreExecutionHook, PreparedMetadataCaptureHook>();
        services.AddSingleton<IModuleWorkerFactory, PreparedTopologyProbeWorkerFactory>();
    }

    private static void ConfigureTopologyProbeServices(IServiceCollection services)
    {
        services.RemoveAll<IModuleWorkerFactory>();
        services.AddSingleton<TopologyProbeState>();
        services.AddSingleton<TopologyProbeLifecycleHook>();
        services.AddSingleton<IModuleExecutionLifecycleHook>(static provider => provider.GetRequiredService<TopologyProbeLifecycleHook>());
        services.AddSingleton<IModuleWorkerFactory, TopologyProbeWorkerFactory>();
    }

    private static ModuleContext CreateProbeContext(string name, TopologyProbeBehavior behavior) => new(
        new ModuleReference(new TopologyProbeModule(behavior) { Name = name }, TopologyProbeModule.MODULE_ID),
        ModuleEnvironment.Default,
        Configuration: null,
        ModuleRequirements.Default);

    private static ValueTask StartAsync(DebugExecutionTopology topology, ModuleExecutionId executionId, ModuleExecutionId? parentExecutionId, string name) =>
        StartAsync(topology, executionId, parentExecutionId, new TestModule { Name = name });

    private static ValueTask StartAsync(DebugExecutionTopology topology, ModuleExecutionId executionId, ModuleExecutionId? parentExecutionId, TestModule module)
    {
        TestLifecycleContext context = CreateContext(executionId, parentExecutionId, module);
        return topology.OnStartedAsync(context, CancellationToken.None);
    }

    private static TestLifecycleContext CreateContext(ModuleExecutionId executionId, ModuleExecutionId? parentExecutionId, TestModule module) =>
        new(executionId, parentExecutionId, TestModule.MODULE_ID, module.Name, module.Group, module);

    private static ModuleExecutionId CreateExecutionId() => new(Guid.NewGuid());


    private static IExecutionTreeNode FindByExecutionId(IExecutionTreeSnapshot snapshot, ModuleExecutionId executionId)
    {
        foreach (IExecutionTreeNode root in snapshot.Roots)
        {
            IExecutionTreeNode? match = FindByExecutionId(root, executionId);
            if (match is not null)
            {
                return match;
            }
        }
        throw new AssertFailedException($"Execution node '{executionId}' was not found.");
    }

    private static IExecutionTreeNode? FindByExecutionId(IExecutionTreeNode node, ModuleExecutionId executionId)
    {
        if (node.ExecutionId == executionId)
        {
            return node;
        }
        foreach (IExecutionTreeNode child in node.Children)
        {
            IExecutionTreeNode? match = FindByExecutionId(child, executionId);
            if (match is not null)
            {
                return match;
            }
        }
        return null;
    }

    private static IExecutionTreeNode FindByName(IExecutionTreeSnapshot snapshot, string name)
    {
        foreach (IExecutionTreeNode root in snapshot.Roots)
        {
            IExecutionTreeNode? match = FindByName(root, name);
            if (match is not null)
            {
                return match;
            }
        }
        throw new AssertFailedException($"Execution node '{name}' was not found.");
    }

    private static IExecutionTreeNode? FindByName(IExecutionTreeNode node, string name)
    {
        if (string.Equals(node.Name, name, StringComparison.Ordinal))
        {
            return node;
        }
        foreach (IExecutionTreeNode child in node.Children)
        {
            IExecutionTreeNode? match = FindByName(child, name);
            if (match is not null)
            {
                return match;
            }
        }
        return null;
    }

    private static T AssertSingle<T>(IReadOnlyCollection<T> values)
    {
        Assert.HasCount(1, values);
        return values.First();
    }

    private sealed record TestModule : ModuleBase
    {
        public const string MODULE_ID = "cyborg.tests.debug-topology.v1";
    }

    private sealed record TestExecutionResult(IModule Module, ModuleExitStatus Status) : IModuleExecutionResult
    {
        public IVariableResolverScope Artifacts => null!;
    }

    private sealed class TestLifecycleContext(
        ModuleExecutionId executionId,
        ModuleExecutionId? parentExecutionId,
        string moduleId,
        string? name,
        string? group,
        IModule module) : IModuleExecutionStartedContext, IModuleExecutionCompletedContext, IModuleExecutionClosedContext
    {
        public ModuleExecutionId ExecutionId { get; } = executionId;

        public ModuleExecutionId? ParentExecutionId { get; } = parentExecutionId;

        public string ModuleId { get; } = moduleId;

        public string? Name { get; } = name;

        public string? Group { get; } = group;

        public IModule Module { get; } = module;

        public IModuleRuntime Runtime => null!;

        public IModuleExecutionResult Result { get; set; } = null!;

        public bool Joined { get; set; }
    }


    private static ModuleArtifacts PreparedArtifacts { get; } = ModuleArtifacts.Default with { Environment = ArtifactModuleEnvironment.Default };

    private sealed record PreparedTopologyProbeModule : ModuleBase, IModule<PreparedTopologyProbeModule>
    {
        public static string ModuleId => "cyborg.tests.debug-topology-prepared.v1";

        public ValueTask<IValidationResult<PreparedTopologyProbeModule>> ValidateAsync(
            IModuleRuntime runtime,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IValidationResult<PreparedTopologyProbeModule>>(
                ValidationResult.Valid(this with
                {
                    Name = "after",
                    Group = "prepared",
                    Artifacts = PreparedArtifacts,
                }));
    }

    private sealed class PreparedTopologyProbeWorkerFactory : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider) =>
            new PreparedTopologyProbeWorker(
                new DefaultWorkerContext<PreparedTopologyProbeModule>((PreparedTopologyProbeModule)moduleReference.Definition, serviceProvider));

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            CreateWorker(new ModuleReference(module, loader), serviceProvider);

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            throw new NotSupportedException();
    }

    private sealed class PreparedTopologyProbeWorker(IWorkerContext<PreparedTopologyProbeModule> context)
        : ModuleWorker<PreparedTopologyProbeModule>(context)
    {
        protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken) =>
            Task.FromResult(runtime.Exit(Success()));
    }

    private sealed class PreparedMetadataCaptureHook(
        IDebugExecutionTopology topology,
        PreparedMetadataObservation observation) : IModulePreExecutionHook
    {
        public int Priority => 0;

        public ValueTask<IModuleExecutionResult<TModule>?> ExecuteAsync<TModule>(
            TModule module,
            IModulePreExecutionContext context,
            CancellationToken cancellationToken)
            where TModule : ModuleBase, IModule<TModule>
        {
            if (string.Equals(context.ModuleId, PreparedTopologyProbeModule.ModuleId, StringComparison.Ordinal))
            {
                IModuleExecutionRuntime executionRuntime = (IModuleExecutionRuntime)context.Runtime;
                ModuleExecutionId executionId = executionRuntime.InvocationContext!.ExecutionId;
                IExecutionTreeNode node = FindByExecutionId(topology.CaptureTree(), executionId);
                observation.Name = node.Name;
                observation.Group = node.Group;
            }
            return ValueTask.FromResult<IModuleExecutionResult<TModule>?>(null);
        }
    }

    private sealed class PreparedMetadataObservation
    {
        public string? Name { get; set; }

        public string? Group { get; set; }
    }

    private sealed record TopologyProbeModule(TopologyProbeBehavior Behavior) : ModuleBase
    {
        public const string MODULE_ID = "cyborg.tests.debug-topology-probe.v1";
    }

    private sealed class TopologyProbeWorkerFactory(TopologyProbeState state) : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider)
        {
            TopologyProbeModule module = (TopologyProbeModule)moduleReference.Definition;
            if (module.Behavior is TopologyProbeBehavior.ActivationFailure)
            {
                throw new InvalidOperationException("Synthetic activation failure before pre-execution hooks.");
            }
            return new TopologyProbeWorker(module, state);
        }

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            CreateWorker(new ModuleReference(module, loader), serviceProvider);

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            throw new NotSupportedException();
    }

    private sealed class TopologyProbeWorker(TopologyProbeModule module, TopologyProbeState state) : IModuleWorker
    {
        public string ModuleId => TopologyProbeModule.MODULE_ID;

        public IModule Module => module;

        async Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            switch (module.Behavior)
            {
                case TopologyProbeBehavior.WaitForSlowStart:
                    await state.SlowStarted.Task.WaitAsync(cancellationToken);
                    break;
                case TopologyProbeBehavior.WaitForRelease:
                    state.SlowStarted.TrySetResult();
                    await state.ReleaseSlow.Task.WaitAsync(cancellationToken);
                    break;
            }
            return new TopologyProbeExecutionResult(module, runtime.Environment.CreateTestArtifactCollection());
        }
    }

    private sealed record TopologyProbeExecutionResult(IModule Module, IVariableResolverScope Artifacts) : IModuleExecutionResult
    {
        public ModuleExitStatus Status => ModuleExitStatus.Success;
    }

    private sealed class TopologyProbeState
    {
        public TaskCompletionSource SlowStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseSlow { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class TopologyProbeLifecycleHook : IModuleExecutionLifecycleHook
    {
        public int Priority => 0;

        public TaskCompletionSource FastCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ActivationFailureStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask OnStartedAsync(IModuleExecutionStartedContext context, CancellationToken cancellationToken)
        {
            if (string.Equals(context.Name, "activation-failure", StringComparison.Ordinal))
            {
                ActivationFailureStarted.TrySetResult();
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask OnCompletedAsync(IModuleExecutionCompletedContext context, CancellationToken cancellationToken)
        {
            if (string.Equals(context.Name, "fast", StringComparison.Ordinal))
            {
                FastCompleted.TrySetResult();
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask OnClosedAsync(IModuleExecutionClosedContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private enum TopologyProbeBehavior
    {
        WaitForSlowStart,
        WaitForRelease,
        ActivationFailure,
    }
}
