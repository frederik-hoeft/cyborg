using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Environments.Artifacts;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Services.Validation;
using Cyborg.Modules.Sidecar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Tests.Sidecar;

[TestClass]
public sealed class SidecarModuleTests : ModuleTestBase
{
    [TestMethod]
    public Task TestValidationAsync_MissingPrimaryModule_IsInvalidAsync() =>
        TestValidationAsync<SidecarModule>(
            """
            {
              "cyborg.modules.sidecar.v1": {}
            }
            """,
            result => MSAssert.IsFalse(result.IsValid));

    [TestMethod]
    public Task TestValidationAsync_OmittedSidecars_IsValidAsync() =>
        TestValidationAsync<SidecarModule>(
            """
            {
              "cyborg.modules.sidecar.v1": {
                "module": {
                  "module": {
                    "cyborg.modules.empty.v1": {
                      "name": "primary"
                    }
                  }
                }
              }
            }
            """,
            result =>
            {
                MSAssert.IsTrue(result.IsValid);
                MSAssert.IsNull(result.Module.Sidecars);
            });

    [TestMethod]
    public Task TestExecutionAsync_PrimaryFailureCancelsCompanionAndReturnsFailedAsync() =>
        TestExecutionAsync(
            """
            {
              "cyborg.modules.sidecar.v1": {
                "module": {
                  "module": {
                    "cyborg.modules.assert.v1": {
                      "name": "primary_assert",
                      "assertion": {
                        "cyborg.modules.condition.is_true.v1": {
                          "variable": "condition"
                        }
                      },
                      "message": "expected failure"
                    }
                  }
                },
                "sidecars": [
                  {
                    "module": {
                      "cyborg.modules.empty.v1": {
                        "name": "companion"
                      }
                    }
                  }
                ]
              }
            }
            """,
            result => MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status),
            environment => environment.SetVariable("condition", false));

    [TestMethod]
    public Task TestExecutionAsync_NoSidecarsReturnsPrimarySuccessAsync() =>
        TestExecutionAsync(
            """
            {
              "cyborg.modules.sidecar.v1": {
                "name": "group",
                "module": {
                  "module": {
                    "cyborg.modules.empty.v1": {
                      "name": "primary"
                    }
                  }
                },
                "sidecars": []
              }
            }
            """,
            result => MSAssert.AreEqual(ModuleExitStatus.Success, result.Status));

    [TestMethod]
    public Task ExecuteAsync_PrimaryCompletionCancelsPendingSidecarAndPublishesBothWritesAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        SidecarProbeRecorder recorder = services.GetRequiredService<SidecarProbeRecorder>();
        IModuleExecutionResult result = await runtime.ExecuteAsync(CreateSidecar(
            CreateProbe("primary", SidecarProbeAction.WaitForSidecarThenSucceed),
            [CreateProbe("lease", SidecarProbeAction.WriteThenWaitForCancel)]), cancellationToken: TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.AreEqual(ModuleExitStatus.Canceled, recorder.SidecarStatus);
        MSAssert.AreEqual("primary", RequireVariable(runtime, "primary_value"));
        MSAssert.AreEqual("lease", RequireVariable(runtime, "lease_value"));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task ExecuteAsync_SidecarFailureCancelsPrimaryAndFailsGroupAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        SidecarProbeRecorder recorder = services.GetRequiredService<SidecarProbeRecorder>();
        IModuleExecutionResult result = await runtime.ExecuteAsync(CreateSidecar(
            CreateProbe("primary", SidecarProbeAction.WaitForCancel),
            [CreateProbe("lease", SidecarProbeAction.FailAfterWrite)]), cancellationToken: TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
        MSAssert.AreEqual(ModuleExitStatus.Canceled, recorder.PrimaryStatus);
        MSAssert.AreEqual(ModuleExitStatus.Failed, recorder.SidecarStatus);
        MSAssert.AreEqual("lease", RequireVariable(runtime, "lease_value"));
        MSAssert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("primary_value", out string? _));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task ExecuteAsync_EarlySidecarSuccessDoesNotCancelPrimaryAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        SidecarProbeRecorder recorder = services.GetRequiredService<SidecarProbeRecorder>();
        Task<IModuleExecutionResult> execution = runtime.ExecuteAsync(CreateSidecar(
            CreateProbe("primary", SidecarProbeAction.WaitForRelease),
            [CreateProbe("lease", SidecarProbeAction.SignalSuccess)]), cancellationToken: TestContext.CancellationToken);
        await recorder.SidecarFinished.Task.WaitAsync(TestContext.CancellationToken);

        MSAssert.IsFalse(recorder.PrimaryFinished);
        recorder.ReleasePrimary.TrySetResult();
        IModuleExecutionResult result = await execution.WaitAsync(TestContext.CancellationToken);
        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.IsTrue(recorder.PrimaryFinished);
        MSAssert.AreEqual(ModuleExitStatus.Success, recorder.SidecarStatus);
    }, ConfigureProbeServices);

    [TestMethod]
    public Task ExecuteAsync_SkippedSidecarDoesNotOverridePrimaryStatusAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IModuleExecutionResult result = await runtime.ExecuteAsync(CreateSidecar(
            CreateProbe("primary", SidecarProbeAction.Succeed),
            [CreateProbe("lease", SidecarProbeAction.Skip)]), cancellationToken: TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.AreEqual("primary", RequireVariable(runtime, "primary_value"));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task ExecuteAsync_CallerCancellationReturnsPrimaryCanceledAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        SidecarProbeRecorder recorder = services.GetRequiredService<SidecarProbeRecorder>();
        recorder.ExpectWaiting(2);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        Task<IModuleExecutionResult> execution = runtime.ExecuteAsync(CreateSidecar(
            CreateProbe("primary", SidecarProbeAction.WaitForCancel),
            [CreateProbe("lease", SidecarProbeAction.WaitForCancel)]), cancellationToken: cancellation.Token);
        await recorder.Waiting.Task.WaitAsync(TestContext.CancellationToken);
        await cancellation.CancelAsync();
        IModuleExecutionResult result = await execution.WaitAsync(TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Canceled, result.Status);
        MSAssert.AreEqual(ModuleExitStatus.Canceled, recorder.PrimaryStatus);
        MSAssert.AreEqual(ModuleExitStatus.Canceled, recorder.SidecarStatus);
    }, ConfigureProbeServices);

    [TestMethod]
    public Task ExecuteAsync_OneOfSeveralSidecarsFailingTerminatesTheGroupAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        SidecarProbeRecorder recorder = services.GetRequiredService<SidecarProbeRecorder>();
        IModuleExecutionResult result = await runtime.ExecuteAsync(CreateSidecar(
            CreateProbe("primary", SidecarProbeAction.WaitForCancel),
            [
                CreateProbe("failing", SidecarProbeAction.FailAfterWrite),
                CreateProbe("pending", SidecarProbeAction.WaitForCancel),
            ]), cancellationToken: TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
        MSAssert.AreEqual(ModuleExitStatus.Canceled, recorder.PrimaryStatus);
        MSAssert.AreEqual(2, recorder.SidecarStatuses.Count);
        MSAssert.IsTrue(recorder.SidecarStatuses.Contains(ModuleExitStatus.Failed));
        MSAssert.IsTrue(recorder.SidecarStatuses.Contains(ModuleExitStatus.Canceled));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task ExecuteAsync_RolledBackSidecarFailureKeepsCommittedPrimaryWritesAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        SidecarProbeRecorder recorder = services.GetRequiredService<SidecarProbeRecorder>();
        IModuleExecutionResult result = await runtime.ExecuteAsync(CreateSidecar(
            CreateProbe("primary", SidecarProbeAction.WriteThenWaitForCancel),
            [CreateProbe("lease", SidecarProbeAction.FailAfterWrite, rollback: true)]), cancellationToken: TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
        MSAssert.AreEqual("primary", RequireVariable(runtime, "primary_value"));
        MSAssert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("lease_value", out string? _));
        MSAssert.AreEqual(ModuleExitStatus.Canceled, recorder.PrimaryStatus);
    }, ConfigureProbeServices);

    [TestMethod]
    public Task ExecuteAsync_ConflictingChildWritesPublishNothingAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        SidecarProbeRecorder recorder = services.GetRequiredService<SidecarProbeRecorder>();
        recorder.ExpectWaiting(2);
        runtime.GlobalEnvironment.SetVariable("shared", "baseline");
        IModuleExecutionResult result = await runtime.ExecuteAsync(CreateSidecar(
            CreateProbe("primary", SidecarProbeAction.WriteSharedThenWait),
            [CreateProbe("lease", SidecarProbeAction.WriteSharedThenWait)]), cancellationToken: TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
        MSAssert.AreEqual("baseline", RequireVariable(runtime, "shared"));
        MSAssert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("primary_only", out string? _));
        MSAssert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("lease_only", out string? _));
    }, ConfigureProbeServices);

    [TestMethod]
    public Task ExecuteAsync_NestedSidecarInsideSidecarSucceedsAsync() =>
        TestExecutionAsync(
            """
            {
              "cyborg.modules.sidecar.v1": {
                "name": "outer",
                "module": {
                  "module": {
                    "cyborg.modules.sidecar.v1": {
                      "name": "inner",
                      "module": {
                        "module": {
                          "cyborg.modules.empty.v1": {
                            "name": "primary"
                          }
                        }
                      }
                    }
                  }
                },
                "sidecars": [
                  {
                    "module": {
                      "cyborg.modules.empty.v1": {
                        "name": "outer_companion"
                      }
                    }
                  }
                ]
              }
            }
            """,
            result => MSAssert.AreEqual(ModuleExitStatus.Success, result.Status));

    [TestMethod]
    public Task ExecuteAsync_SidecarInsideParallelSucceedsAsync() =>
        TestExecutionAsync(
            """
            {
              "cyborg.modules.parallel.v1": {
                "branches": [
                  {
                    "module": {
                      "cyborg.modules.sidecar.v1": {
                        "module": {
                          "module": {
                            "cyborg.modules.empty.v1": {
                              "name": "primary"
                            }
                          }
                        }
                      }
                    }
                  },
                  {
                    "module": {
                      "cyborg.modules.empty.v1": {
                        "name": "sibling"
                      }
                    }
                  }
                ]
              }
            }
            """,
            result => MSAssert.AreEqual(ModuleExitStatus.Success, result.Status));

    private static string RequireVariable(IModuleRuntime runtime, string name)
    {
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable(name, out string? value));
        return value ?? throw new AssertFailedException($"Variable '{name}' was null.");
    }

    private static ModuleReference CreateSidecar(ModuleContext primary, IReadOnlyList<ModuleContext> sidecars) =>
        new(new SidecarModule(primary, sidecars) { Name = "group" }, SidecarModule.ModuleId);

    private static ModuleContext CreateProbe(string name, SidecarProbeAction action, bool rollback = false) =>
        new(
            new ModuleReference(CreateProbeModule(name, action, rollback), SidecarProbeModule.ModuleId),
            new ModuleEnvironment { Scope = EnvironmentScope.Global },
            Configuration: null,
            ModuleRequirements.Default);

    private static SidecarProbeModule CreateProbeModule(string name, SidecarProbeAction action, bool rollback) =>
        new(action)
        {
            Name = name,
            Artifacts = new ModuleArtifacts(
                Namespace: null,
                ExitStatusName: "$?",
                Environment: ArtifactModuleEnvironment.Default,
                DecompositionStrategy: DecompositionStrategy.LeavesOnly,
                PublishNullValues: false),
            Transaction = rollback ? new ModuleTransactionSettings(TransactionOnError.Rollback) : ModuleTransactionSettings.Default,
        };

    private static void ConfigureProbeServices(IServiceCollection services)
    {
        services.AddSingleton<SidecarProbeRecorder>();
        services.RemoveAll<IModuleWorkerFactory>();
        services.AddSingleton<IModuleWorkerFactory>(static serviceProvider => new SidecarProbeWorkerFactory(
            new DefaultModuleWorkerFactory(
                serviceProvider.GetRequiredService<IModuleLoaderRegistry>(),
                serviceProvider.GetServices<IModuleLoader>()),
            serviceProvider.GetRequiredService<SidecarProbeRecorder>()));
    }

    private enum SidecarProbeAction
    {
        WaitForSidecarThenSucceed,
        WriteThenWaitForCancel,
        WaitForCancel,
        FailAfterWrite,
        WaitForRelease,
        SignalSuccess,
        Succeed,
        Skip,
        WriteSharedThenWait,
    }

    private sealed record SidecarProbeModule(SidecarProbeAction Action) : ModuleBase, IModule<SidecarProbeModule>
    {
        public const string MODULE_ID = "cyborg.tests.sidecar-probe.v1";

        public static string ModuleId => MODULE_ID;

        public ValueTask<IValidationResult<SidecarProbeModule>> ValidateAsync(IModuleRuntime runtime, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(serviceProvider);
            return ValueTask.FromResult<IValidationResult<SidecarProbeModule>>(new ValidationResult<SidecarProbeModule>(this, []));
        }
    }

    private sealed class SidecarProbeWorkerFactory(IModuleWorkerFactory fallback, SidecarProbeRecorder recorder) : IModuleWorkerFactory
    {
        private readonly SidecarModuleLoader _sidecarLoader = new();

        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider)
        {
            if (moduleReference.Definition is SidecarModule sidecar)
            {
                return ((IModuleLoader<SidecarModule>)_sidecarLoader).CreateWorker(sidecar, serviceProvider);
            }
            if (moduleReference.Definition is SidecarProbeModule probe)
            {
                return new SidecarProbeWorker(new DefaultWorkerContext<SidecarProbeModule>(probe, serviceProvider), recorder);
            }
            return fallback.CreateWorker(moduleReference, serviceProvider);
        }

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            CreateWorker(new ModuleReference(module, loader), serviceProvider);

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            throw new NotSupportedException();
    }

    private sealed class SidecarProbeWorker(IWorkerContext<SidecarProbeModule> context, SidecarProbeRecorder recorder) : ModuleWorker<SidecarProbeModule>(context)
    {
        protected override async Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            switch (Module.Action)
            {
                case SidecarProbeAction.WaitForSidecarThenSucceed:
                    await recorder.SidecarWrote.Task.WaitAsync(cancellationToken);
                    runtime.Environment.SetVariable("primary_value", "primary");
                    recorder.PrimaryStatus = ModuleExitStatus.Success;
                    return runtime.Exit(Success());
                case SidecarProbeAction.WriteThenWaitForCancel:
                    runtime.Environment.SetVariable($"{Module.Name}_value", Module.Name);
                    recorder.SidecarWrote.TrySetResult();
                    recorder.SignalWaiting();
                    await WaitForCancellationAsync(cancellationToken);
                    return runtime.Exit(Success());
                case SidecarProbeAction.WaitForCancel:
                    recorder.SignalWaiting();
                    await WaitForCancellationAsync(cancellationToken);
                    return runtime.Exit(Success());
                case SidecarProbeAction.FailAfterWrite:
                    runtime.Environment.SetVariable($"{Module.Name}_value", Module.Name);
                    recorder.RecordSidecar(ModuleExitStatus.Failed);
                    return runtime.Exit(Failed());
                case SidecarProbeAction.WaitForRelease:
                    await recorder.ReleasePrimary.Task.WaitAsync(cancellationToken);
                    recorder.PrimaryFinished = true;
                    recorder.PrimaryStatus = ModuleExitStatus.Success;
                    return runtime.Exit(Success());
                case SidecarProbeAction.SignalSuccess:
                    recorder.RecordSidecar(ModuleExitStatus.Success);
                    recorder.SidecarFinished.TrySetResult();
                    return runtime.Exit(Success());
                case SidecarProbeAction.Succeed:
                    runtime.Environment.SetVariable("primary_value", "primary");
                    recorder.PrimaryStatus = ModuleExitStatus.Success;
                    return runtime.Exit(Success());
                case SidecarProbeAction.Skip:
                    recorder.RecordSidecar(ModuleExitStatus.Skipped);
                    return runtime.Exit(Skipped());
                case SidecarProbeAction.WriteSharedThenWait:
                    runtime.Environment.SetVariable("shared", Module.Name);
                    runtime.Environment.SetVariable($"{Module.Name}_only", Module.Name);
                    recorder.SignalWaiting();
                    await recorder.Waiting.Task.WaitAsync(cancellationToken);
                    if (Module.Name == "primary")
                    {
                        recorder.PrimaryStatus = ModuleExitStatus.Success;
                    }
                    else
                    {
                        recorder.RecordSidecar(ModuleExitStatus.Success);
                    }
                    return runtime.Exit(Success());
                default:
                    throw new ArgumentOutOfRangeException(nameof(Module.Action), Module.Action, null);
            }
        }

        private async Task WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Record(ModuleExitStatus.Canceled);
                throw;
            }
        }

        private void Record(ModuleExitStatus status)
        {
            if (Module.Name == "primary")
            {
                recorder.PrimaryStatus = status;
                return;
            }

            recorder.RecordSidecar(status);
        }
    }

    private sealed class SidecarProbeRecorder
    {
        private int _waiting;
        private int _expectedWaiting = 1;

        public TaskCompletionSource SidecarWrote { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SidecarFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleasePrimary { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool PrimaryFinished { get; set; }

        public ModuleExitStatus? PrimaryStatus { get; set; }

        public ModuleExitStatus? SidecarStatus { get; private set; }

        public List<ModuleExitStatus> SidecarStatuses { get; } = [];

        public void ExpectWaiting(int count) => _expectedWaiting = count;

        public void SignalWaiting()
        {
            if (Interlocked.Increment(ref _waiting) >= _expectedWaiting)
            {
                Waiting.TrySetResult();
            }
        }

        public void RecordSidecar(ModuleExitStatus status)
        {
            SidecarStatus = status;
            lock (SidecarStatuses)
            {
                SidecarStatuses.Add(status);
            }
        }
    }
}
