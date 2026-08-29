using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class ParallelModuleRuntimeTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task ExecuteConcurrentlyAsync_BranchesOverlapShareBaselineAndJoinInDeclarationOrderAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.GlobalEnvironment.SetVariable("baseline", "initial");
        ParallelProbeRecorder recorder = services.GetRequiredService<ParallelProbeRecorder>();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        IReadOnlyList<IModuleExecutionResult> results = await runtime.ExecuteConcurrentlyAsync(
            [
                CreateContext("first", ParallelProbeAction.OverlapAndWrite),
                CreateContext("second", ParallelProbeAction.OverlapAndWrite),
            ],
            timeout.Token);

        Assert.HasCount(2, results);
        Assert.AreEqual("first", results[0].Module.Name);
        Assert.AreEqual("second", results[1].Module.Name);
        Assert.AreSequenceEqual(["second", "first"], recorder.CompletionOrder);
        Assert.HasCount(2, recorder.Observations);
        Assert.IsTrue(recorder.Observations.All(static observation => observation.Baseline == "initial"));
        Assert.IsTrue(recorder.Observations.All(static observation => !observation.SawSiblingBeforeWrite));
        Assert.IsFalse(recorder.FirstSawSecondAfterSecondCompleted);
        Assert.AreNotSame(recorder.Observations[0].ScopedProbe, recorder.Observations[1].ScopedProbe);
        Assert.AreSame(recorder.Observations[0].SingletonProbe, recorder.Observations[1].SingletonProbe);
        Assert.IsTrue(recorder.Observations.All(static observation => observation.ScopedProbe.IsDisposed));
        Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("first_value", out string? firstValue));
        Assert.AreEqual("first", firstValue);
        Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("second_value", out string? secondValue));
        Assert.AreEqual("second", secondValue);
    }, ConfigureParallelProbeServices);

    [TestMethod]
    public Task ExecuteConcurrentlyAsync_SetRemoveConflictLeavesOwnerStateUnchangedAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.GlobalEnvironment.SetVariable("shared", "baseline");

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            runtime.ExecuteConcurrentlyAsync(
                [
                    CreateContext("setter", ParallelProbeAction.SetConflict),
                    CreateContext("remover", ParallelProbeAction.RemoveConflict),
                ],
                TestContext.CancellationToken));

        Assert.Contains("RuntimeEnvironmentTransactionParticipant", exception.Message);
        Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("shared", out string? shared));
        Assert.AreEqual("baseline", shared);
        Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("setter_only", out string? _));
        Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("remover_only", out string? _));
    }, ConfigureParallelProbeServices);

    [TestMethod]
    public Task ExecuteConcurrentlyAsync_CancellationReachesEveryStartedBranchAndDisposesScopesAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ParallelProbeRecorder recorder = services.GetRequiredService<ParallelProbeRecorder>();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        Task<IReadOnlyList<IModuleExecutionResult>> execution = runtime.ExecuteConcurrentlyAsync(
            [
                CreateContext("first", ParallelProbeAction.WaitForCancellation),
                CreateContext("second", ParallelProbeAction.WaitForCancellation),
            ],
            cancellation.Token);
        await recorder.WaitForAllStartedAsync();
        await cancellation.CancelAsync();
        IReadOnlyList<IModuleExecutionResult> results = await execution;

        Assert.HasCount(2, results);
        Assert.IsTrue(results.All(static result => result.Status == ModuleExitStatus.Canceled));
        Assert.AreEqual(2, recorder.CancellationObservations);
        Assert.HasCount(2, recorder.Observations);
        Assert.IsTrue(recorder.Observations.All(static observation => observation.ScopedProbe.IsDisposed));
    }, ConfigureParallelProbeServices);

    private static ModuleContext CreateContext(string name, ParallelProbeAction action) => new(
        new ModuleReference(new ParallelProbeModule(action) { Name = name }, ParallelProbeModule.ModuleId),
        new ModuleEnvironment { Scope = EnvironmentScope.Global },
        Configuration: null,
        ModuleRequirements.Default);

    private static void ConfigureParallelProbeServices(IServiceCollection services)
    {
        services.RemoveAll<IModuleWorkerFactory>();
        services.AddSingleton<ParallelProbeRecorder>();
        services.AddSingleton<ParallelSingletonProbe>();
        services.AddScoped<ParallelScopedProbe>();
        services.AddSingleton<IModuleWorkerFactory, ParallelProbeWorkerFactory>();
    }

    private enum ParallelProbeAction
    {
        OverlapAndWrite,
        SetConflict,
        RemoveConflict,
        WaitForCancellation,
    }

    private sealed record ParallelProbeModule(ParallelProbeAction Action) : ModuleBase, IModule
    {
        public static string ModuleId => "cyborg.tests.parallel-probe.v1";
    }

    private sealed class ParallelProbeWorkerFactory(ParallelProbeRecorder recorder) : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider)
        {
            ParallelProbeModule module = (ParallelProbeModule)moduleReference.Definition;
            ParallelScopedProbe scopedProbe = serviceProvider.GetRequiredService<ParallelScopedProbe>();
            ParallelSingletonProbe singletonProbe = serviceProvider.GetRequiredService<ParallelSingletonProbe>();
            return new ParallelProbeWorker(module, scopedProbe, singletonProbe, recorder);
        }

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            CreateWorker(new ModuleReference(module, loader), serviceProvider);

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            throw new NotSupportedException();
    }

    private sealed class ParallelProbeWorker(
        ParallelProbeModule module,
        ParallelScopedProbe scopedProbe,
        ParallelSingletonProbe singletonProbe,
        ParallelProbeRecorder recorder) : IModuleWorker
    {
        public string ModuleId => ParallelProbeModule.ModuleId;

        public IModule Module => module;

        async Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            recorder.RecordObservation(module.Name!, runtime.Environment, scopedProbe, singletonProbe);
            switch (module.Action)
            {
                case ParallelProbeAction.OverlapAndWrite:
                    await recorder.ReachOverlapBarrierAsync();
                    if (module.Name == "second")
                    {
                        runtime.Environment.SetVariable("second_value", "second");
                        recorder.RecordCompleted("second");
                        recorder.ReleaseFirst();
                    }
                    else
                    {
                        await recorder.WaitForFirstReleaseAsync();
                        recorder.FirstSawSecondAfterSecondCompleted = runtime.Environment.TryResolveVariable("second_value", out string? _);
                        runtime.Environment.SetVariable("first_value", "first");
                        recorder.RecordCompleted("first");
                    }
                    break;
                case ParallelProbeAction.SetConflict:
                    runtime.Environment.SetVariable("shared", "setter");
                    runtime.Environment.SetVariable("setter_only", "value");
                    break;
                case ParallelProbeAction.RemoveConflict:
                    Assert.IsTrue(runtime.Environment.TryRemoveVariable("shared"));
                    runtime.Environment.SetVariable("remover_only", "value");
                    break;
                case ParallelProbeAction.WaitForCancellation:
                    await recorder.ReachCancellationBarrierAsync();
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    }
                    finally
                    {
                        recorder.RecordCancellationObserved();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module.Action), module.Action, null);
            }

            return new ParallelProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateTestArtifactCollection());
        }
    }

    private sealed class ParallelProbeRecorder
    {
        private readonly TaskCompletionSource _allCancellationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _allOverlapStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<string> _completionOrder = [];
        private readonly List<ParallelProbeObservation> _observations = [];
        private int _cancellationObservations;
        private int _cancellationStarted;
        private int _overlapStarted;

        public IReadOnlyList<string> CompletionOrder
        {
            get
            {
                lock (_completionOrder)
                {
                    return [.. _completionOrder];
                }
            }
        }

        public IReadOnlyList<ParallelProbeObservation> Observations
        {
            get
            {
                lock (_observations)
                {
                    return [.. _observations];
                }
            }
        }

        public int CancellationObservations => Volatile.Read(ref _cancellationObservations);

        public bool FirstSawSecondAfterSecondCompleted { get; set; }

        public void RecordObservation(
            string name,
            IRuntimeEnvironment environment,
            ParallelScopedProbe scopedProbe,
            ParallelSingletonProbe singletonProbe)
        {
            environment.TryResolveVariable("baseline", out string? baseline);
            bool sawSibling = name == "first"
                ? environment.TryResolveVariable("second_value", out string? _)
                : environment.TryResolveVariable("first_value", out string? _);
            lock (_observations)
            {
                _observations.Add(new ParallelProbeObservation(name, baseline, sawSibling, scopedProbe, singletonProbe));
            }
        }

        public Task ReachOverlapBarrierAsync()
        {
            if (Interlocked.Increment(ref _overlapStarted) == 2)
            {
                _allOverlapStarted.TrySetResult();
            }
            return _allOverlapStarted.Task;
        }

        public void ReleaseFirst() => _releaseFirst.TrySetResult();

        public Task WaitForFirstReleaseAsync() => _releaseFirst.Task;

        public void RecordCompleted(string name)
        {
            lock (_completionOrder)
            {
                _completionOrder.Add(name);
            }
        }

        public Task ReachCancellationBarrierAsync()
        {
            if (Interlocked.Increment(ref _cancellationStarted) == 2)
            {
                _allCancellationStarted.TrySetResult();
            }
            return _allCancellationStarted.Task;
        }

        public Task WaitForAllStartedAsync() => _allCancellationStarted.Task;

        public void RecordCancellationObserved() => Interlocked.Increment(ref _cancellationObservations);
    }

    private sealed record ParallelProbeObservation(
        string Name,
        string? Baseline,
        bool SawSiblingBeforeWrite,
        ParallelScopedProbe ScopedProbe,
        ParallelSingletonProbe SingletonProbe);

    private sealed class ParallelScopedProbe : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class ParallelSingletonProbe;

    private sealed record ParallelProbeExecutionResult(
        IModule Module,
        ModuleExitStatus Status,
        IVariableResolverScope Artifacts) : IModuleExecutionResult;
}
