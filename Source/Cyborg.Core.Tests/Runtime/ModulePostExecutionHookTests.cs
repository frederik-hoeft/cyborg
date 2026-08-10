using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Hooks;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class ModulePostExecutionHookTests : CyborgCoreTestBase
{
    [TestMethod]
    public async Task Test_ExecuteAsync_PostHookFailure_DoesNotShortCircuitPipelineOrChangeResultAsync()
    {
        RecordingPostExecutionHook failingHook = new(priority: 0, throwOnExecute: true);
        RecordingPostExecutionHook trailingHook = new(priority: 1);

        await TestWithDIAsync(async services =>
        {
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IModuleExecutionResult result = await runtime.ExecuteAsync(new ProbeModuleWorker(ProbeBehavior.Success), cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(ModuleExitStatus.Success, result.Status);
            Assert.AreEqual(1, failingHook.CallCount);
            Assert.AreEqual(1, trailingHook.CallCount);
            Assert.AreSequenceEqual([ModuleExitStatus.Success], failingHook.ObservedStatuses);
            Assert.AreSequenceEqual([ModuleExitStatus.Success], trailingHook.ObservedStatuses);
        }, services =>
        {
            services.AddSingleton<IModulePostExecutionHook>(failingHook);
            services.AddSingleton<IModulePostExecutionHook>(trailingHook);
        });
    }

    [TestMethod]
    public async Task Test_ExecuteAsync_WhenWorkerThrows_PostHooksObserveFailedResultAsync()
    {
        RecordingPostExecutionHook hook = new(priority: 0);

        await TestWithDIAsync(async services =>
        {
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IModuleExecutionResult result = await runtime.ExecuteAsync(new ProbeModuleWorker(ProbeBehavior.Throw), cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(ModuleExitStatus.Failed, result.Status);
            Assert.AreEqual(1, hook.CallCount);
            Assert.AreSequenceEqual([ModuleExitStatus.Failed], hook.ObservedStatuses);
        }, services => services.AddSingleton<IModulePostExecutionHook>(hook));
    }

    [TestMethod]
    public async Task Test_ExecuteAsync_WhenWorkerIsCanceled_PostHooksObserveCanceledResultAsync()
    {
        RecordingPostExecutionHook hook = new(priority: 0);
        using CancellationTokenSource cancellationSource = new();
        cancellationSource.Cancel();

        await TestWithDIAsync(async services =>
        {
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IModuleExecutionResult result = await runtime.ExecuteAsync(new ProbeModuleWorker(ProbeBehavior.Cancel), cancellationToken: cancellationSource.Token);

            Assert.AreEqual(ModuleExitStatus.Canceled, result.Status);
            Assert.AreEqual(1, hook.CallCount);
            Assert.AreSequenceEqual([ModuleExitStatus.Canceled], hook.ObservedStatuses);
            Assert.AreSequenceEqual([false], hook.ObservedCancellationStates);
        }, services => services.AddSingleton<IModulePostExecutionHook>(hook));
    }

    [TestMethod]
    public async Task Test_ExecuteAsync_PostHookPipeline_IsResolvedPerDispatchAsync()
    {
        int createdHooks = 0;

        await TestWithDIAsync(async services =>
        {
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            await runtime.ExecuteAsync(new ProbeModuleWorker(ProbeBehavior.Success), cancellationToken: TestContext.CancellationToken);
            await runtime.ExecuteAsync(new ProbeModuleWorker(ProbeBehavior.Success), cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(2, createdHooks);
        }, services => services.AddTransient<IModulePostExecutionHook>(_ =>
        {
            createdHooks++;
            return new RecordingPostExecutionHook(priority: 0);
        }));
    }

    [TestMethod]
    public async Task Test_ExecuteAsync_PostHookPipelineResolutionFailure_DoesNotChangeResultAsync()
    {
        await TestWithDIAsync(async services =>
        {
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IModuleExecutionResult result = await runtime.ExecuteAsync(new ProbeModuleWorker(ProbeBehavior.Success), cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        }, services => services.AddTransient<IModulePostExecutionHook>(static _ => throw new InvalidOperationException("Synthetic post-hook construction failure.")));
    }

    private sealed class RecordingPostExecutionHook(int priority, bool throwOnExecute = false) : IModulePostExecutionHook
    {
        public int Priority => priority;

        public int CallCount { get; private set; }

        public List<ModuleExitStatus> ObservedStatuses { get; } = [];

        public List<bool> ObservedCancellationStates { get; } = [];

        public ValueTask ExecuteAsync(IModulePostExecutionContext context, CancellationToken cancellationToken)
        {
            CallCount++;
            ObservedStatuses.Add(context.Result.Status);
            ObservedCancellationStates.Add(cancellationToken.IsCancellationRequested);
            if (throwOnExecute)
            {
                throw new InvalidOperationException("Synthetic post-execution hook failure.");
            }
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ProbeModuleWorker(ProbeBehavior behavior) : IModuleWorker
    {
        public string ModuleId => ProbeModule.ModuleId;

        public IModule Module { get; } = new ProbeModule();

        Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            return behavior switch
            {
                ProbeBehavior.Success => Task.FromResult<IModuleExecutionResult>(new ProbeExecutionResult(Module, ModuleExitStatus.Success, runtime.Environment.CreateArtifactCollection())),
                ProbeBehavior.Throw => Task.FromException<IModuleExecutionResult>(new InvalidOperationException("Synthetic module failure.")),
                ProbeBehavior.Cancel => Task.FromException<IModuleExecutionResult>(new OperationCanceledException("Synthetic module cancellation.", cancellationToken)),
                _ => throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "Unknown probe behavior.")
            };
        }
    }

    private sealed record ProbeModule : ModuleBase, IModule
    {
        public static string ModuleId => "cyborg.tests.post-hook-probe.v1";
    }

    private sealed record ProbeExecutionResult(IModule Module, ModuleExitStatus Status, IVariableResolverScope Artifacts) : IModuleExecutionResult;

    private enum ProbeBehavior
    {
        Success,
        Throw,
        Cancel
    }
}
