using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Hooks;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Services.Pipelines;
using Cyborg.Core.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class ModuleExecutionLifecycleTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task Test_ExecutionIdentity_NestedInvocationUsesStableParentAndWorkerIdentityAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        ProbeExecutionState state = services.GetRequiredService<ProbeExecutionState>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleReference root = CreateReference("root", ProbeBehavior.ExecuteChild);

        IModuleExecutionResult result = await runtime.ExecuteAsync(root, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        LifecycleObservation rootStarted = hook.GetSingle("root", LifecycleEvent.Started);
        LifecycleObservation childStarted = hook.GetSingle("child", LifecycleEvent.Started);
        Assert.IsNull(rootStarted.ParentExecutionId);
        Assert.AreEqual(rootStarted.ExecutionId, childStarted.ParentExecutionId);
        Assert.AreNotEqual(rootStarted.ExecutionId, childStarted.ExecutionId);
        Assert.AreEqual(rootStarted.ExecutionId, state.GetWorkerExecutionId("root"));
        Assert.AreEqual(childStarted.ExecutionId, state.GetWorkerExecutionId("child"));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task Test_ExecutionIdentity_ModuleContextConfigurationIsChildOfOwningInvocationAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        ProbeExecutionState state = services.GetRequiredService<ProbeExecutionState>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleContext moduleContext = new(
            CreateReference("main", ProbeBehavior.Success),
            ModuleEnvironment.Default,
            CreateReference("configuration", ProbeBehavior.Success),
            ModuleRequirements.Default);

        IModuleExecutionResult result = await runtime.ExecuteAsync(moduleContext, TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        LifecycleObservation mainStarted = hook.GetSingle("main", LifecycleEvent.Started);
        LifecycleObservation configurationStarted = hook.GetSingle("configuration", LifecycleEvent.Started);
        Assert.IsNull(mainStarted.ParentExecutionId);
        Assert.AreEqual(mainStarted.ExecutionId, configurationStarted.ParentExecutionId);
        Assert.AreEqual(mainStarted.ExecutionId, state.GetWorkerExecutionId("main"));
        Assert.AreEqual(configurationStarted.ExecutionId, state.GetWorkerExecutionId("configuration"));
        Assert.HasCount(2, hook.Observations.Where(static observation => observation.Event == LifecycleEvent.Started).ToArray());
    }, ConfigureProbeServices);

    [TestMethod]
    public Task Test_ExecutionIdentity_ParallelSiblingsUseDistinctIdsWithSameParentAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        ProbeExecutionState state = services.GetRequiredService<ProbeExecutionState>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleReference root = CreateReference("root", ProbeBehavior.ExecuteParallelChildren);

        IModuleExecutionResult result = await runtime.ExecuteAsync(root, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        LifecycleObservation rootStarted = hook.GetSingle("root", LifecycleEvent.Started);
        LifecycleObservation firstStarted = hook.GetSingle("first", LifecycleEvent.Started);
        LifecycleObservation secondStarted = hook.GetSingle("second", LifecycleEvent.Started);
        Assert.AreEqual(rootStarted.ExecutionId, firstStarted.ParentExecutionId);
        Assert.AreEqual(rootStarted.ExecutionId, secondStarted.ParentExecutionId);
        Assert.AreNotEqual(firstStarted.ExecutionId, secondStarted.ExecutionId);
        Assert.AreEqual(firstStarted.ExecutionId, state.GetWorkerExecutionId("first"));
        Assert.AreEqual(secondStarted.ExecutionId, state.GetWorkerExecutionId("second"));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task Test_Lifecycle_DefiniteResultsCompleteBeforeJoinedCloseAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        IModuleExecutionResult success = await runtime.ExecuteAsync(CreateReference("success", ProbeBehavior.Success), cancellationToken: TestContext.CancellationToken);
        IModuleExecutionResult failed = await runtime.ExecuteAsync(CreateReference("failed", ProbeBehavior.FailedResult), cancellationToken: TestContext.CancellationToken);
        IModuleExecutionResult canceled = await runtime.ExecuteAsync(CreateReference("canceled", ProbeBehavior.CanceledResult), cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, success.Status);
        Assert.AreEqual(ModuleExitStatus.Failed, failed.Status);
        Assert.AreEqual(ModuleExitStatus.Canceled, canceled.Status);
        AssertLifecycle(hook, "success", ModuleExitStatus.Success, joined: true);
        AssertLifecycle(hook, "failed", ModuleExitStatus.Failed, joined: true);
        AssertLifecycle(hook, "canceled", ModuleExitStatus.Canceled, joined: true);
    }, ConfigureProbeServices);

    [TestMethod]
    public Task Test_Lifecycle_ExceptionBeforeResultStartsThenClosesWithoutCompletionAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            runtime.ExecuteAsync(CreateReference("activation-failure", ProbeBehavior.ActivationFailure), cancellationToken: TestContext.CancellationToken));

        AssertExceptionalLifecycle(hook, "activation-failure");
    }, ConfigureProbeServices);

    [TestMethod]
    public Task Test_Lifecycle_WorkerCancellationProducesDefiniteCanceledLifecycleAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        using CancellationTokenSource cancellationSource = new();
        await cancellationSource.CancelAsync();

        IModuleExecutionResult result = await runtime.ExecuteAsync(
            CreateReference("worker-cancellation", ProbeBehavior.ThrowCancellation),
            cancellationToken: cancellationSource.Token);

        Assert.AreEqual(ModuleExitStatus.Canceled, result.Status);
        AssertLifecycle(hook, "worker-cancellation", ModuleExitStatus.Canceled, joined: true);
        LifecycleObservation[] observations = hook.GetForModule("worker-cancellation");
        Assert.IsTrue(observations[0].CancellationRequested);
        Assert.IsFalse(observations[1].CancellationRequested);
        Assert.IsFalse(observations[2].CancellationRequested);
    }, ConfigureProbeServices);

    [TestMethod]
    public Task Test_Lifecycle_ExceptionalCancellationBeforeResultStartsThenClosesAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            runtime.ExecuteAsync(CreateReference("activation-cancellation", ProbeBehavior.ActivationCancellation), cancellationToken: TestContext.CancellationToken));

        AssertExceptionalLifecycle(hook, "activation-cancellation");
    }, ConfigureProbeServices);

    [TestMethod]
    public Task Test_Lifecycle_ParallelEnvironmentPreparationFailureStartsThenClosesAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleContext moduleContext = new(
            CreateReference("missing-environment", ProbeBehavior.Success),
            new ModuleEnvironment
            {
                Scope = EnvironmentScope.Reference,
                Name = "does-not-exist",
            },
            Configuration: null,
            ModuleRequirements.Default);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            runtime.ExecuteConcurrentlyAsync([moduleContext], TestContext.CancellationToken));

        AssertExceptionalLifecycle(hook, "missing-environment");
    }, ConfigureProbeServices);

    [TestMethod]
    public Task Test_Lifecycle_ParallelCompletedChildRemainsOpenUntilForkClosesAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        ProbeExecutionState state = services.GetRequiredService<ProbeExecutionState>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        Task<IReadOnlyList<IModuleExecutionResult>> execution = runtime.ExecuteConcurrentlyAsync(
            [
                CreateContext("fast", ProbeBehavior.WaitForSlowStart),
                CreateContext("slow", ProbeBehavior.WaitForRelease),
            ],
            TestContext.CancellationToken);

        await hook.FastCompleted.Task.WaitAsync(TestContext.CancellationToken);

        Assert.HasCount(1, hook.Get("fast", LifecycleEvent.Completed));
        Assert.HasCount(0, hook.Get("fast", LifecycleEvent.Closed));
        Assert.HasCount(1, hook.Get("slow", LifecycleEvent.Started));

        state.ReleaseSlow.TrySetResult();
        IReadOnlyList<IModuleExecutionResult> results = await execution;

        Assert.HasCount(2, results);
        AssertLifecycle(hook, "fast", ModuleExitStatus.Success, joined: true);
        AssertLifecycle(hook, "slow", ModuleExitStatus.Success, joined: true);
    }, ConfigureProbeServices);

    [TestMethod]
    public Task Test_Lifecycle_ParallelJoinConflictCompletesThenClosesDiscardedAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => runtime.ExecuteConcurrentlyAsync(
            [
                CreateContext("first", ProbeBehavior.WriteGlobal, "one"),
                CreateContext("second", ProbeBehavior.WriteGlobal, "two"),
            ],
            TestContext.CancellationToken));

        AssertLifecycle(hook, "first", ModuleExitStatus.Success, joined: false);
        AssertLifecycle(hook, "second", ModuleExitStatus.Success, joined: false);
    }, ConfigureProbeServices);

    [TestMethod]
    public Task Test_Lifecycle_ObserverFailureDoesNotChangeExecutionOrLaterObserverDeliveryAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        IModuleExecutionResult result = await runtime.ExecuteAsync(CreateReference("survives", ProbeBehavior.Success), cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        AssertLifecycle(hook, "survives", ModuleExitStatus.Success, joined: true);
    }, services =>
    {
        ConfigureProbeServices(services);
        services.AddSingleton<IModuleExecutionLifecycleHook>(new ThrowingExecutionLifecycleHook(priority: -1));
    });

    [TestMethod]
    public Task Test_ExecutionIdentity_LoadedRootModuleUsesOneRootInvocationAsync() => TestWithDIAsync(async services =>
    {
        RecordingExecutionLifecycleHook hook = services.GetRequiredService<RecordingExecutionLifecycleHook>();
        ProbeExecutionState state = services.GetRequiredService<ProbeExecutionState>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleContext moduleContext = CreateContext("loaded-root", ProbeBehavior.Success);
        ModuleConfigurationLoadResult configuration = new(moduleContext);
        IRuntimeEnvironment environment = runtime.PrepareEnvironment(moduleContext.Environment);

        IModuleExecutionResult result = await runtime.ExecuteRootModuleAsync(configuration, environment, TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        LifecycleObservation started = hook.GetSingle("loaded-root", LifecycleEvent.Started);
        Assert.IsNull(started.ParentExecutionId);
        Assert.AreEqual(started.ExecutionId, state.GetWorkerExecutionId("loaded-root"));
        Assert.HasCount(1, hook.Observations.Where(static observation => observation.Event == LifecycleEvent.Started).ToArray());
    }, ConfigureProbeServices);

    private static void ConfigureProbeServices(IServiceCollection services)
    {
        services.RemoveAll<IModuleWorkerFactory>();
        services.AddSingleton<ProbeExecutionState>();
        services.AddSingleton<RecordingExecutionLifecycleHook>();
        services.AddSingleton<IModuleExecutionLifecycleHook>(static provider => provider.GetRequiredService<RecordingExecutionLifecycleHook>());
        services.AddSingleton<IModuleWorkerFactory, ProbeWorkerFactory>();
    }

    private static ModuleReference CreateReference(string name, ProbeBehavior behavior, string? value = null) =>
        new(new ProbeModule(behavior, value) { Name = name }, ProbeModule.MODULE_ID);

    private static ModuleContext CreateContext(string name, ProbeBehavior behavior, string? value = null) =>
        new(CreateReference(name, behavior, value), ModuleEnvironment.Default, Configuration: null, ModuleRequirements.Default);

    private static void AssertLifecycle(RecordingExecutionLifecycleHook hook, string name, ModuleExitStatus status, bool joined)
    {
        LifecycleObservation[] observations = hook.GetForModule(name);
        Assert.AreSequenceEqual(
            [LifecycleEvent.Started, LifecycleEvent.Completed, LifecycleEvent.Closed],
            observations.Select(static observation => observation.Event).ToArray());
        Assert.AreEqual(status, observations[1].Status);
        Assert.AreEqual(joined, observations[2].Joined);
        Assert.IsFalse(observations[1].CancellationRequested);
        Assert.IsFalse(observations[2].CancellationRequested);
    }

    private static LifecycleObservation[] AssertExceptionalLifecycle(RecordingExecutionLifecycleHook hook, string name)
    {
        LifecycleObservation[] observations = hook.GetForModule(name);
        Assert.AreSequenceEqual(
            [LifecycleEvent.Started, LifecycleEvent.Closed],
            observations.Select(static observation => observation.Event).ToArray());
        Assert.IsFalse(observations[1].Joined);
        Assert.IsFalse(observations[1].CancellationRequested);
        return observations;
    }

    private sealed record ProbeModule(ProbeBehavior Behavior, string? Value) : ModuleBase
    {
        public const string MODULE_ID = "cyborg.tests.execution-lifecycle-probe.v1";
    }

    private sealed class ProbeWorkerFactory(ProbeExecutionState state) : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider)
        {
            ProbeModule module = (ProbeModule)moduleReference.Definition;
            return module.Behavior switch
            {
                ProbeBehavior.ActivationFailure => throw new InvalidOperationException("Synthetic activation failure."),
                ProbeBehavior.ActivationCancellation => throw new OperationCanceledException("Synthetic activation cancellation."),
                _ => new ProbeWorker(module, state),
            };
        }

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            CreateWorker(new ModuleReference(module, loader), serviceProvider);

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            throw new NotSupportedException();
    }

    private sealed class ProbeWorker(ProbeModule module, ProbeExecutionState state) : IModuleWorker
    {
        public string ModuleId => ProbeModule.MODULE_ID;

        public IModule Module => module;

        async Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            Assert.IsInstanceOfType<IModuleExecutionRuntime>(runtime);
            IModuleExecutionRuntime executionRuntime = (IModuleExecutionRuntime)runtime;
            ModuleInvocationContext invocation = executionRuntime.InvocationContext
                ?? throw new InvalidOperationException("Probe worker did not receive an invocation context.");
            state.RecordWorkerExecution(module.Name ?? string.Empty, invocation.ExecutionId);

            switch (module.Behavior)
            {
                case ProbeBehavior.ExecuteChild:
                    IModuleExecutionResult childResult = await runtime.ExecuteAsync(
                        CreateReference("child", ProbeBehavior.Success),
                        cancellationToken: cancellationToken);
                    Assert.AreEqual(ModuleExitStatus.Success, childResult.Status);
                    break;
                case ProbeBehavior.ExecuteParallelChildren:
                    IReadOnlyList<IModuleExecutionResult> parallelResults = await runtime.ExecuteConcurrentlyAsync(
                        [
                            CreateContext("first", ProbeBehavior.Success),
                            CreateContext("second", ProbeBehavior.Success),
                        ],
                        cancellationToken);
                    Assert.IsTrue(parallelResults.All(static result => result.Status == ModuleExitStatus.Success));
                    break;
                case ProbeBehavior.WaitForSlowStart:
                    await state.SlowStarted.Task.WaitAsync(cancellationToken);
                    break;
                case ProbeBehavior.WaitForRelease:
                    state.SlowStarted.TrySetResult();
                    await state.ReleaseSlow.Task.WaitAsync(cancellationToken);
                    break;
                case ProbeBehavior.WriteGlobal:
                    runtime.GlobalEnvironment.SetVariable("shared", module.Value ?? string.Empty);
                    break;
                case ProbeBehavior.ThrowCancellation:
                    cancellationToken.ThrowIfCancellationRequested();
                    break;
            }

            ModuleExitStatus status = module.Behavior switch
            {
                ProbeBehavior.FailedResult => ModuleExitStatus.Failed,
                ProbeBehavior.CanceledResult => ModuleExitStatus.Canceled,
                _ => ModuleExitStatus.Success,
            };
            return new ProbeExecutionResult(module, status, runtime.Environment.CreateTestArtifactCollection());
        }
    }

    private sealed record ProbeExecutionResult(IModule Module, ModuleExitStatus Status, IVariableResolverScope Artifacts) : IModuleExecutionResult;

    private sealed class ProbeExecutionState
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, ModuleExecutionId> _workerExecutionIds = [];

        public TaskCompletionSource SlowStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseSlow { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void RecordWorkerExecution(string name, ModuleExecutionId executionId)
        {
            lock (_lock)
            {
                _workerExecutionIds[name] = executionId;
            }
        }

        public ModuleExecutionId GetWorkerExecutionId(string name)
        {
            lock (_lock)
            {
                return _workerExecutionIds[name];
            }
        }
    }

    private sealed class RecordingExecutionLifecycleHook : IModuleExecutionLifecycleHook
    {
        private readonly object _lock = new();
        private readonly List<LifecycleObservation> _observations = [];

        public int Priority => 0;

        public TaskCompletionSource FastCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<LifecycleObservation> Observations
        {
            get
            {
                lock (_lock)
                {
                    return _observations.ToArray();
                }
            }
        }

        public ValueTask OnStartedAsync(IModuleExecutionStartedContext context, CancellationToken cancellationToken)
        {
            Record(new LifecycleObservation(
                LifecycleEvent.Started,
                context.ExecutionId,
                context.ParentExecutionId,
                context.Name,
                Status: null,
                Joined: null,
                cancellationToken.IsCancellationRequested));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnCompletedAsync(IModuleExecutionCompletedContext context, CancellationToken cancellationToken)
        {
            Record(new LifecycleObservation(
                LifecycleEvent.Completed,
                context.ExecutionId,
                context.ParentExecutionId,
                context.Name,
                context.Result.Status,
                Joined: null,
                cancellationToken.IsCancellationRequested));
            if (string.Equals(context.Name, "fast", StringComparison.Ordinal))
            {
                FastCompleted.TrySetResult();
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask OnClosedAsync(IModuleExecutionClosedContext context, CancellationToken cancellationToken)
        {
            Record(new LifecycleObservation(
                LifecycleEvent.Closed,
                context.ExecutionId,
                context.ParentExecutionId,
                context.Name,
                Status: null,
                context.Joined,
                cancellationToken.IsCancellationRequested));
            return ValueTask.CompletedTask;
        }

        public LifecycleObservation[] GetForModule(string name)
        {
            lock (_lock)
            {
                return [.. _observations.Where(observation => string.Equals(observation.ModuleName, name, StringComparison.Ordinal))];
            }
        }

        public LifecycleObservation[] Get(string name, LifecycleEvent lifecycleEvent)
        {
            lock (_lock)
            {
                return
                [
                    .. _observations.Where(observation =>
                        observation.Event == lifecycleEvent
                        && string.Equals(observation.ModuleName, name, StringComparison.Ordinal)),
                ];
            }
        }

        public LifecycleObservation GetSingle(string name, LifecycleEvent lifecycleEvent)
        {
            LifecycleObservation[] observations = Get(name, lifecycleEvent);
            Assert.HasCount(1, observations);
            return observations[0];
        }

        private void Record(LifecycleObservation observation)
        {
            lock (_lock)
            {
                _observations.Add(observation);
            }
        }
    }

    private sealed class ThrowingExecutionLifecycleHook(int priority) : IModuleExecutionLifecycleHook
    {
        public int Priority => priority;

        public ValueTask OnStartedAsync(IModuleExecutionStartedContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Synthetic started observer failure.");

        public ValueTask OnCompletedAsync(IModuleExecutionCompletedContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Synthetic completed observer failure.");

        public ValueTask OnClosedAsync(IModuleExecutionClosedContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Synthetic closed observer failure.");
    }

    private sealed record LifecycleObservation(
        LifecycleEvent Event,
        ModuleExecutionId ExecutionId,
        ModuleExecutionId? ParentExecutionId,
        string? ModuleName,
        ModuleExitStatus? Status,
        bool? Joined,
        bool CancellationRequested);

    private enum LifecycleEvent
    {
        Started,
        Completed,
        Closed,
    }

    private enum ProbeBehavior
    {
        Success,
        FailedResult,
        CanceledResult,
        ExecuteChild,
        ExecuteParallelChildren,
        ActivationFailure,
        ActivationCancellation,
        ThrowCancellation,
        WaitForSlowStart,
        WaitForRelease,
        WriteGlobal,
    }
}
