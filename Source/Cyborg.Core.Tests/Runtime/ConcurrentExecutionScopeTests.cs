using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Hooks;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Services.Transactions;
using Cyborg.Core.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class ConcurrentExecutionScopeTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task CloseAsync_OwnerAndChildWritesStayIsolatedUntilReconcileAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ScopeRecorder recorder = services.GetRequiredService<ScopeRecorder>();
        runtime.GlobalEnvironment.SetVariable("before", "baseline");
        IConcurrentExecutionScope scope = runtime.OpenConcurrentExecution();
        try
        {
            runtime.GlobalEnvironment.SetVariable("during", "owner");
            IConcurrentModuleExecution child = await scope.StartAsync(CreateContext("child", ScopeProbeAction.ReadAndWrite), TestContext.CancellationToken);
            IModuleExecutionResult result = await child.Completion.WaitAsync(TestContext.CancellationToken);

            Assert.AreEqual(ModuleExitStatus.Success, result.Status);
            Assert.AreEqual("baseline", recorder.SeenBefore);
            Assert.IsFalse(recorder.SawDuring);
            Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("during", out string? during));
            Assert.AreEqual("owner", during);
            Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("child_value", out string? _));

            await scope.CloseAsync(TestContext.CancellationToken);
        }
        finally
        {
            await scope.DisposeAsync();
        }

        Assert.AreEqual("owner", RequireVariable(runtime, "during"));
        Assert.AreEqual("child", RequireVariable(runtime, "child_value"));
        Assert.AreEqual("baseline", RequireVariable(runtime, "before"));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task CloseAsync_OwnerAndChildConflictPublishesNothingAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.GlobalEnvironment.SetVariable("shared", "baseline");
        IConcurrentExecutionScope scope = runtime.OpenConcurrentExecution();
        try
        {
            runtime.GlobalEnvironment.SetVariable("shared", "owner");
            runtime.GlobalEnvironment.SetVariable("owner_only", "owner");
            IConcurrentModuleExecution child = await scope.StartAsync(CreateContext("child", ScopeProbeAction.WriteConflict), TestContext.CancellationToken);
            Assert.AreEqual(ModuleExitStatus.Success, (await child.Completion.WaitAsync(TestContext.CancellationToken)).Status);

            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.CloseAsync(TestContext.CancellationToken));
            Assert.Contains("RuntimeEnvironmentTransactionParticipant", exception.Message);
        }
        finally
        {
            await scope.DisposeAsync();
        }

        Assert.AreEqual("baseline", RequireVariable(runtime, "shared"));
        Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("owner_only", out string? _));
        Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("child_only", out string? _));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task Cancel_OneChildCanFinishWhileAnotherKeepsRunningAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ScopeRecorder recorder = services.GetRequiredService<ScopeRecorder>();
        IConcurrentExecutionScope scope = runtime.OpenConcurrentExecution();
        try
        {
            IConcurrentModuleExecution canceled = await scope.StartAsync(CreateContext("canceled", ScopeProbeAction.WriteThenWait), TestContext.CancellationToken);
            IConcurrentModuleExecution running = await scope.StartAsync(CreateContext("running", ScopeProbeAction.WaitForRelease), TestContext.CancellationToken);
            await recorder.Waiting.Task.WaitAsync(TestContext.CancellationToken);
            canceled.Cancel();
            IModuleExecutionResult canceledResult = await canceled.Completion.WaitAsync(TestContext.CancellationToken);

            Assert.AreEqual(ModuleExitStatus.Canceled, canceledResult.Status);
            Assert.IsFalse(running.Completion.IsCompleted);
            recorder.Release.TrySetResult();
            IModuleExecutionResult runningResult = await running.Completion.WaitAsync(TestContext.CancellationToken);
            Assert.AreEqual(ModuleExitStatus.Success, runningResult.Status);
            Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("running_value", out string? _));

            await scope.CloseAsync(TestContext.CancellationToken);
        }
        finally
        {
            await scope.DisposeAsync();
        }

        Assert.AreEqual("canceled", RequireVariable(runtime, "canceled_value"));
        Assert.AreEqual("running", RequireVariable(runtime, "running_value"));
        Assert.IsTrue(recorder.ScopedServices.All(static service => service.IsDisposed));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task CloseAsync_RollbackWithholdsCanceledChildAndCommitPublishesItAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ScopeRecorder recorder = services.GetRequiredService<ScopeRecorder>();
        recorder.ExpectWaiting(2);
        IConcurrentExecutionScope scope = runtime.OpenConcurrentExecution();
        try
        {
            IConcurrentModuleExecution committed = await scope.StartAsync(
                CreateContext("committed", ScopeProbeAction.WriteThenWait, rollback: false),
                TestContext.CancellationToken);
            IConcurrentModuleExecution rolledBack = await scope.StartAsync(
                CreateContext("rolled-back", ScopeProbeAction.WriteThenWait, rollback: true),
                TestContext.CancellationToken);
            await recorder.Waiting.Task.WaitAsync(TestContext.CancellationToken);
            committed.Cancel();
            rolledBack.Cancel();
            Assert.AreEqual(ModuleExitStatus.Canceled, (await committed.Completion.WaitAsync(TestContext.CancellationToken)).Status);
            Assert.AreEqual(ModuleExitStatus.Canceled, (await rolledBack.Completion.WaitAsync(TestContext.CancellationToken)).Status);
            await scope.CloseAsync(TestContext.CancellationToken);
        }
        finally
        {
            await scope.DisposeAsync();
        }

        Assert.AreEqual("committed", RequireVariable(runtime, "committed_value"));
        Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("rolled-back_value", out string? _));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task DisposeAsync_DiscardsContinuationAndChildWritesAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ScopeRecorder recorder = services.GetRequiredService<ScopeRecorder>();
        runtime.GlobalEnvironment.SetVariable("baseline", "kept");
        IConcurrentExecutionScope scope = runtime.OpenConcurrentExecution();
        IConcurrentModuleExecution child = await scope.StartAsync(CreateContext("child", ScopeProbeAction.WriteThenWait), TestContext.CancellationToken);
        await recorder.Waiting.Task.WaitAsync(TestContext.CancellationToken);
        runtime.GlobalEnvironment.SetVariable("owner_discarded", "owner");
        await scope.DisposeAsync();

        Assert.AreEqual("kept", RequireVariable(runtime, "baseline"));
        Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("owner_discarded", out string? _));
        Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("child_value", out string? _));
        Assert.IsTrue(child.Completion.IsCompleted);
        runtime.GlobalEnvironment.SetVariable("after", "ok");
        Assert.AreEqual("ok", RequireVariable(runtime, "after"));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task ExecuteAsync_DuringScopeJoinsIntoContinuationAndNotIntoSiblingsAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ScopeRecorder recorder = services.GetRequiredService<ScopeRecorder>();
        IConcurrentExecutionScope scope = runtime.OpenConcurrentExecution();
        try
        {
            runtime.GlobalEnvironment.SetVariable("during", "owner");
            IConcurrentModuleExecution child = await scope.StartAsync(CreateContext("concurrent", ScopeProbeAction.ReadDuringThenWait), TestContext.CancellationToken);
            await recorder.Waiting.Task.WaitAsync(TestContext.CancellationToken);
            IModuleExecutionResult sequential = await runtime.ExecuteAsync(CreateContext("sequential", ScopeProbeAction.ReadDuringAndWriteSequential), TestContext.CancellationToken);

            Assert.AreEqual(ModuleExitStatus.Success, sequential.Status);
            Assert.IsFalse(recorder.SawDuring);
            Assert.AreEqual("sequential", RequireVariable(runtime, "sequential_value"));
            Assert.AreEqual("owner", RequireVariable(runtime, "during_seen"));
            Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("concurrent_value", out string? _));
            recorder.Release.TrySetResult();
            await child.Completion.WaitAsync(TestContext.CancellationToken);
            Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("concurrent_value", out string? _));
            await scope.CloseAsync(TestContext.CancellationToken);
        }
        finally
        {
            await scope.DisposeAsync();
        }

        Assert.AreEqual("concurrent", RequireVariable(runtime, "concurrent_value"));
        Assert.AreEqual("sequential", RequireVariable(runtime, "sequential_value"));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task CloseAsync_NestedScopePublishesIntoOuterContinuationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ScopeRecorder recorder = services.GetRequiredService<ScopeRecorder>();
        IConcurrentExecutionScope outer = runtime.OpenConcurrentExecution();
        try
        {
            runtime.GlobalEnvironment.SetVariable("outer_value", "outer");
            IConcurrentModuleExecution outerChild = await outer.StartAsync(CreateContext("outer-child", ScopeProbeAction.ReadOuterAndWrite), TestContext.CancellationToken);
            IConcurrentExecutionScope inner = runtime.OpenConcurrentExecution();
            try
            {
                runtime.GlobalEnvironment.SetVariable("inner_value", "inner");
                IConcurrentModuleExecution innerChild = await inner.StartAsync(CreateContext("inner-child", ScopeProbeAction.WriteInner), TestContext.CancellationToken);
                Assert.AreEqual(ModuleExitStatus.Success, (await innerChild.Completion.WaitAsync(TestContext.CancellationToken)).Status);
                Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("inner_child", out string? _));
                await inner.CloseAsync(TestContext.CancellationToken);
            }
            finally
            {
                await inner.DisposeAsync();
            }

            Assert.AreEqual("inner", RequireVariable(runtime, "inner_value"));
            Assert.AreEqual("inner-child", RequireVariable(runtime, "inner_child"));
            IModuleExecutionResult outerResult = await outerChild.Completion.WaitAsync(TestContext.CancellationToken);
            Assert.AreEqual(ModuleExitStatus.Success, outerResult.Status);
            Assert.IsFalse(recorder.SawOuter);
            Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("outer_child", out string? _));
            await outer.CloseAsync(TestContext.CancellationToken);
        }
        finally
        {
            await outer.DisposeAsync();
        }

        Assert.AreEqual("outer", RequireVariable(runtime, "outer_value"));
        Assert.AreEqual("outer-child", RequireVariable(runtime, "outer_child"));
        Assert.AreEqual("inner-child", RequireVariable(runtime, "inner_child"));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task CloseAsync_CompletedChildStaysOpenUntilScopeReconcilesAsync() => TestWithDIAsync(async services =>
    {
        RecordingScopeHook hook = services.GetRequiredService<RecordingScopeHook>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IConcurrentExecutionScope scope = runtime.OpenConcurrentExecution();
        try
        {
            IConcurrentModuleExecution child = await scope.StartAsync(CreateContext("child", ScopeProbeAction.WriteInner), TestContext.CancellationToken);
            await child.Completion.WaitAsync(TestContext.CancellationToken);

            Assert.AreEqual(1, hook.Count("child", "completed"));
            Assert.AreEqual(0, hook.Count("child", "closed"));
            await scope.CloseAsync(TestContext.CancellationToken);
        }
        finally
        {
            await scope.DisposeAsync();
        }

        Assert.AreEqual(1, hook.Count("child", "started"));
        Assert.AreEqual(1, hook.Count("child", "completed"));
        Assert.AreEqual(1, hook.Count("child", "closed"));
        Assert.IsTrue(hook.Joined("child"));
    }, ConfigureLifecycleServices);

    [TestMethod]
    public Task ExecuteAsync_OwnerRegistryAndTransactionalServiceJoinWithChildAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ScopeRecorder recorder = services.GetRequiredService<ScopeRecorder>();
        IModuleExecutionResult result = await runtime.ExecuteAsync(CreateContext("owner", ScopeProbeAction.Coordinate), TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.IsFalse(recorder.SawChildModuleBeforeClose);
        Assert.IsTrue(recorder.SawOwnerModuleAfterClose);
        Assert.IsTrue(recorder.SawChildModuleAfterClose);
        Assert.AreEqual(11, recorder.CounterAfterClose);
        Assert.AreEqual("child", RequireVariable(runtime, "child_value"));
    }, ConfigureTransactionalServices);

    private static string RequireVariable(IModuleRuntime runtime, string name)
    {
        Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable(name, out string? value));
        return value;
    }

    private static ModuleContext CreateContext(string name, ScopeProbeAction action, bool rollback = false)
    {
        ScopeProbeModule module = new(action)
        {
            Name = name,
            Transaction = rollback ? new ModuleTransactionSettings(TransactionOnError.Rollback) : ModuleTransactionSettings.Default,
        };
        return new ModuleContext(
            new ModuleReference(module, ScopeProbeModule.ModuleId),
            new ModuleEnvironment { Scope = EnvironmentScope.Global },
            Configuration: null,
            ModuleRequirements.Default);
    }

    private static void ConfigureProbeServices(IServiceCollection services)
    {
        services.RemoveAll<IModuleWorkerFactory>();
        services.AddSingleton<ScopeRecorder>();
        services.AddScoped<ScopeDisposable>();
        services.AddSingleton<IModuleWorkerFactory, ScopeProbeWorkerFactory>();
    }

    private static void ConfigureLifecycleServices(IServiceCollection services)
    {
        ConfigureProbeServices(services);
        services.AddSingleton<RecordingScopeHook>();
        services.AddSingleton<IModuleExecutionLifecycleHook>(static provider => provider.GetRequiredService<RecordingScopeHook>());
    }

    private static void ConfigureTransactionalServices(IServiceCollection services)
    {
        ConfigureProbeServices(services);
        services.AddSingleton<TransactionalServiceParticipant, ScopeCounterParticipant>();
        services.AddScoped<ScopeCounterService>();
    }

    private enum ScopeProbeAction
    {
        ReadAndWrite,
        WriteConflict,
        WriteThenWait,
        WaitForRelease,
        ReadDuringThenWait,
        ReadDuringAndWriteSequential,
        ReadOuterAndWrite,
        WriteInner,
        Coordinate,
        AddChildModule,
    }

    private sealed record ScopeProbeModule(ScopeProbeAction Action) : ModuleBase
    {
        public const string MODULE_ID = "cyborg.tests.concurrent-scope.v1";

        public static string ModuleId => MODULE_ID;
    }

    private sealed class ScopeProbeWorkerFactory(ScopeRecorder recorder) : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider)
        {
            ScopeProbeModule module = (ScopeProbeModule)moduleReference.Definition;
            ScopeDisposable? scopedService = serviceProvider.GetService<ScopeDisposable>();
            IModuleRegistry? registry = serviceProvider.GetService<IModuleRegistry>();
            if (module.Action == ScopeProbeAction.Coordinate)
            {
                return new CoordinatingProbeWorker(
                    module,
                    registry ?? throw new InvalidOperationException("The coordinating probe requires a module registry."),
                    serviceProvider.GetRequiredService<ScopeCounterService>(),
                    recorder);
            }
            return new ScopeProbeWorker(module, scopedService, registry, recorder);
        }

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            CreateWorker(new ModuleReference(module, loader), serviceProvider);

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            throw new NotSupportedException();
    }

    private sealed class ScopeProbeWorker(ScopeProbeModule module, ScopeDisposable? scopedService, IModuleRegistry? registry, ScopeRecorder recorder) : IModuleWorker
    {
        public string ModuleId => ScopeProbeModule.ModuleId;

        public IModule Module => module;

        public async Task<IModuleExecutionResult> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            if (scopedService is not null)
            {
                recorder.ScopedServices.Add(scopedService);
            }

            switch (module.Action)
            {
                case ScopeProbeAction.ReadAndWrite:
                    recorder.SeenBefore = runtime.GlobalEnvironment.TryResolveVariable("before", out string? before) ? before : null;
                    recorder.SawDuring = runtime.GlobalEnvironment.TryResolveVariable("during", out string? _);
                    runtime.GlobalEnvironment.SetVariable("child_value", "child");
                    break;
                case ScopeProbeAction.WriteConflict:
                    runtime.GlobalEnvironment.SetVariable("shared", "child");
                    runtime.GlobalEnvironment.SetVariable("child_only", "child");
                    break;
                case ScopeProbeAction.WriteThenWait:
                    runtime.GlobalEnvironment.SetVariable($"{module.Name}_value", module.Name);
                    recorder.SignalWaiting();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    break;
                case ScopeProbeAction.WaitForRelease:
                    recorder.SignalWaiting();
                    await recorder.Release.Task.WaitAsync(cancellationToken);
                    runtime.GlobalEnvironment.SetVariable("running_value", "running");
                    break;
                case ScopeProbeAction.ReadDuringThenWait:
                    recorder.SawDuring = runtime.GlobalEnvironment.TryResolveVariable("during", out string? _);
                    runtime.GlobalEnvironment.SetVariable("concurrent_value", "concurrent");
                    recorder.SignalWaiting();
                    await recorder.Release.Task.WaitAsync(cancellationToken);
                    break;
                case ScopeProbeAction.ReadDuringAndWriteSequential:
                    runtime.GlobalEnvironment.SetVariable(
                        "during_seen",
                        runtime.GlobalEnvironment.TryResolveVariable("during", out string? during) ? during : "missing");
                    runtime.GlobalEnvironment.SetVariable("sequential_value", "sequential");
                    break;
                case ScopeProbeAction.ReadOuterAndWrite:
                    recorder.SawOuter = runtime.GlobalEnvironment.TryResolveVariable("outer_value", out string? _);
                    runtime.GlobalEnvironment.SetVariable("outer_child", "outer-child");
                    break;
                case ScopeProbeAction.WriteInner:
                    runtime.GlobalEnvironment.SetVariable("inner_child", "inner-child");
                    break;
                case ScopeProbeAction.AddChildModule:
                    Assert.IsTrue((registry ?? throw new InvalidOperationException("The child probe requires a module registry.")).TryAddModule(
                        "child_added",
                        CreateContext("child_added", ScopeProbeAction.WriteInner)));
                    runtime.GlobalEnvironment.SetVariable("child_value", "child");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module.Action), module.Action, null);
            }

            return new ScopeProbeResult(module, ModuleExitStatus.Success, runtime.Environment.CreateTestArtifactCollection());
        }

        Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken) =>
            ExecuteAsync(runtime, cancellationToken);
    }

    private sealed class CoordinatingProbeWorker(
        ScopeProbeModule module,
        IModuleRegistry registry,
        ScopeCounterService counter,
        ScopeRecorder recorder) : IModuleWorker
    {
        public string ModuleId => ScopeProbeModule.ModuleId;

        public IModule Module => module;

        public async Task<IModuleExecutionResult> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            recorder.OwnerRegistry = registry;
            IConcurrentExecutionScope scope = runtime.OpenConcurrentExecution();
            try
            {
                counter.Set(11);
                Assert.IsTrue(registry.TryAddModule("owner_added", CreateContext("owner_added", ScopeProbeAction.WriteInner)));
                IConcurrentModuleExecution child = await scope.StartAsync(CreateContext("child", ScopeProbeAction.AddChildModule), cancellationToken);
                Assert.AreEqual(ModuleExitStatus.Success, (await child.Completion.WaitAsync(cancellationToken)).Status);
                recorder.SawChildModuleBeforeClose = registry.TryGetModule("child_added", out ModuleContext? _);
                Assert.AreEqual(11, counter.Value);
                await scope.CloseAsync(cancellationToken);
                recorder.CounterAfterClose = counter.Value;
                recorder.SawOwnerModuleAfterClose = registry.TryGetModule("owner_added", out ModuleContext? _);
                recorder.SawChildModuleAfterClose = registry.TryGetModule("child_added", out ModuleContext? _);
            }
            finally
            {
                await scope.DisposeAsync();
            }

            return new ScopeProbeResult(module, ModuleExitStatus.Success, runtime.Environment.CreateTestArtifactCollection());
        }

        Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken) =>
            ExecuteAsync(runtime, cancellationToken);
    }

    private sealed class ScopeRecorder
    {
        private int _waiting;

        private int _expectedWaiting = 1;

        public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<ScopeDisposable> ScopedServices { get; } = [];

        public string? SeenBefore { get; set; }

        public bool SawDuring { get; set; }

        public bool SawOuter { get; set; }

        public bool SawChildModuleBeforeClose { get; set; }

        public bool SawOwnerModuleAfterClose { get; set; }

        public bool SawChildModuleAfterClose { get; set; }

        public int CounterAfterClose { get; set; }

        public IModuleRegistry? OwnerRegistry { get; set; }

        public void ExpectWaiting(int count) => _expectedWaiting = count;

        public void SignalWaiting()
        {
            if (Interlocked.Increment(ref _waiting) >= _expectedWaiting)
            {
                Waiting.TrySetResult();
            }
        }
    }

    private sealed class ScopeDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class RecordingScopeHook : IModuleExecutionLifecycleHook
    {
        private readonly List<(string? Name, string Event, bool Joined)> _events = [];

        public int Priority => 0;

        public int Count(string name, string eventName)
        {
            lock (_events)
            {
                return _events.Count(entry => entry.Name == name && entry.Event == eventName);
            }
        }

        public bool Joined(string name)
        {
            lock (_events)
            {
                return _events.Any(entry => entry.Name == name && entry.Event == "closed" && entry.Joined);
            }
        }

        public ValueTask OnStartedAsync(IModuleExecutionStartedContext context, CancellationToken cancellationToken)
        {
            Add(context.Name, "started", joined: false);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnCompletedAsync(IModuleExecutionCompletedContext context, CancellationToken cancellationToken)
        {
            Add(context.Name, "completed", joined: false);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnClosedAsync(IModuleExecutionClosedContext context, CancellationToken cancellationToken)
        {
            Add(context.Name, "closed", context.Joined);
            return ValueTask.CompletedTask;
        }

        private void Add(string? name, string eventName, bool joined)
        {
            lock (_events)
            {
                _events.Add((name, eventName, joined));
            }
        }
    }

    private sealed class ScopeCounterService(ITransactionalServiceContext context)
    {
        private readonly ITransactionalServiceState<ScopeCounterState> _state =
            context.GetState<ScopeCounterParticipant, ScopeCounterState>();

        public int Value => _state.Read(static state => state.Value);

        public void Set(int value) => _state.Mutate(state => state.Set(value));
    }

    private sealed class ScopeCounterParticipant : TransactionalServiceParticipant<ScopeCounterState>
    {
        protected override ScopeCounterState CreateRootState() => new(0, isChanged: false);

        protected override TransactionalServiceFork<ScopeCounterState> CreateFork(ScopeCounterState ownerState) => new ScopeCounterFork(ownerState);
    }

    private sealed class ScopeCounterFork(ScopeCounterState ownerState) : TransactionalServiceFork<ScopeCounterState>
    {
        private readonly int _baseline = ownerState.Value;

        private readonly bool _ownerChanged = ownerState.IsChanged;

        public override ScopeCounterState CreateBranch() => new(_baseline, isChanged: false);

        public override bool TryPrepareMerge(
            IReadOnlyList<ScopeCounterState> contributors,
            ITransactionalServiceConflictResolver conflictResolver,
            [NotNullWhen(true)] out ScopeCounterState? candidate)
        {
            ArgumentNullException.ThrowIfNull(contributors);
            ArgumentNullException.ThrowIfNull(conflictResolver);
            List<int> changed = [];
            for (int i = 0; i < contributors.Count; i++)
            {
                if (contributors[i].IsChanged)
                {
                    changed.Add(i);
                }
            }

            int value = _baseline;
            if (changed.Count == 1)
            {
                value = contributors[changed[0]].Value;
            }
            else if (changed.Count > 1)
            {
                if (!conflictResolver.TryResolve(nameof(ScopeCounterState.Value), changed, out int selected))
                {
                    candidate = null;
                    return false;
                }
                value = contributors[selected].Value;
            }

            candidate = new ScopeCounterState(value, _ownerChanged || changed.Count > 0);
            return true;
        }
    }

    private sealed class ScopeCounterState(int value, bool isChanged)
    {
        public int Value { get; private set; } = value;

        public bool IsChanged { get; private set; } = isChanged;

        public void Set(int value)
        {
            Value = value;
            IsChanged = true;
        }
    }

    private sealed record ScopeProbeResult(IModule Module, ModuleExitStatus Status, IVariableResolverScope Artifacts) : IModuleExecutionResult;
}
