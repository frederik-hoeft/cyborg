using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Runtime.Services.Validation;
using Cyborg.Core.Services.Default;
using Cyborg.Core.TestAdapter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class WorkflowDebuggerTests : CyborgCoreTestBase
{
    protected override void ConfigureServices(IServiceCollection services, IJabServiceDiscovery jabServiceDiscovery)
    {
        base.ConfigureServices(services, jabServiceDiscovery);
        services.RemoveAll<IDebugBranchControl>();
        services.AddSingleton<IDebugBranchControl, TestDebugBranchControl>();
    }

    protected override void BuildConfiguration(IConfigurationBuilder configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        IServiceSelectionKey<IDebugFrontend> frontendKey = configuration.ServiceProvider.GetRequiredService<IServiceSelectionKey<IDebugFrontend>>();
        configuration.AddDictionary(dict => dict.AddEntry(frontendKey.Key, "test"));
    }

    [TestMethod]
    public Task Test_EvaluatePreExecutionAsync_WhenInactive_ReturnsContinueWithoutFrontendAsync() => TestWithDIAsync(async services =>
    {
        IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(
            ProbeModule.ModuleId,
            ValidationResult.Valid(new ProbeModule()),
            runtime,
            services,
            TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Continue, action);
    });

    [TestMethod]
    public Task Test_EvaluatePreExecutionAsync_WhenBreakpointMatches_InvokesFrontendAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();
        ScriptedFrontend frontend = GetFrontend<ScriptedFrontend>(services);
        breakpoints.Add("probe");
        ProbeModule module = new() { Name = "probe-step" };

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(
            ProbeModule.ModuleId,
            ValidationResult.Valid(module),
            runtime,
            services,
            TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(1, frontend.PauseCount);
        Assert.AreEqual("cyborg.tests.probe.v1 name=probe-step", frontend.LastIdentity);
    }, static services => services.AddSingleton<IDebugFrontend>(new ScriptedFrontend(DebugResumeAction.Continue)));

    [TestMethod]
    public Task Test_EvaluatePreExecutionAsync_CancelFromFrontend_PropagatesAndClearsStepAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IDebugBranchControl branchControl = services.GetRequiredService<IDebugBranchControl>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();
        branchControl.Step();
        breakpoints.Add(".*");

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(
            ProbeModule.ModuleId,
            ValidationResult.Valid(new ProbeModule()),
            runtime,
            services,
            TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Cancel, action);
        Assert.IsFalse(branchControl.IsStepping);
    }, static services => services.AddSingleton<IDebugFrontend>(new ScriptedFrontend(DebugResumeAction.Cancel)));

    [TestMethod]
    public Task Test_StepAction_UsesBranchControlWithoutRegisteringWildcardBreakpointAsync() => TestWithDIAsync(
        assertion: async services =>
        {
            IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
            IDebugBranchControl branchControl = services.GetRequiredService<IDebugBranchControl>();
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();
            ScriptedSequenceFrontend frontend = GetFrontend<ScriptedSequenceFrontend>(services);
            int persistentId = breakpoints.Add("first");

            await debugger.EvaluatePreExecutionAsync(
                ProbeModule.ModuleId,
                ValidationResult.Valid(new ProbeModule { Name = "first" }),
                runtime,
                services,
                TestContext.CancellationToken);

            Assert.IsTrue(branchControl.IsStepping);
            IReadOnlyList<BreakpointExpression> registeredBreakpoints = breakpoints.ToList();
            Assert.HasCount(1, registeredBreakpoints);
            Assert.AreEqual(persistentId, registeredBreakpoints[0].Id);
            Assert.IsFalse(registeredBreakpoints[0].IsOneShot);
            Assert.IsTrue(breakpoints.Remove(persistentId));

            DebugResumeAction secondAction = await debugger.EvaluatePreExecutionAsync(
                ProbeModule.ModuleId,
                ValidationResult.Valid(new ProbeModule { Name = "second" }),
                runtime,
                services,
                TestContext.CancellationToken);

            Assert.AreEqual(DebugResumeAction.Continue, secondAction);
            Assert.AreEqual(2, frontend.PauseCount);
            Assert.IsFalse(branchControl.IsStepping);
            Assert.AreEqual(0, breakpoints.Count);
        },
        configureServices: static services => services.AddSingleton<IDebugFrontend>(new ScriptedSequenceFrontend([
            DebugResumeAction.Step,
            DebugResumeAction.Continue,
        ])));

    [TestMethod]
    public Task Test_EvaluatePreExecutionAsync_InvalidResult_PassesPreparedModuleAndErrorsToFrontendAsync() => TestWithDIAsync(
        assertion: async services =>
        {
            IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();
            ScriptedFrontend frontend = GetFrontend<ScriptedFrontend>(services);
            breakpoints.Add("probe");
            ProbeModule module = new() { Name = "probe-invalid" };
            ValidationError error = new(nameof(ProbeModule.Name), "test-rule", "The probe is invalid.");
            IValidationResult<ProbeModule> validationResult = ValidationResult.Invalid(module, [error]);

            DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(
                ProbeModule.ModuleId,
                validationResult,
                runtime,
                services,
                TestContext.CancellationToken);

            Assert.AreEqual(DebugResumeAction.Continue, action);
            Assert.IsFalse(frontend.LastIsValid);
            Assert.AreEqual(module, frontend.LastModule);
            Assert.AreSequenceEqual([error], frontend.LastValidationErrors);
        },
        configureServices: static services => services.AddSingleton<IDebugFrontend>(new ScriptedFrontend(DebugResumeAction.Continue)));

    [TestMethod]
    public Task Test_EvaluatePreExecutionAsync_WhenNoFrontendIsAvailable_ReturnsContinueAsync() => TestWithDIAsync(
        assertion: async services =>
        {
            IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();
            breakpoints.Add("probe");

            DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(
                ProbeModule.ModuleId,
                ValidationResult.Valid(new ProbeModule()),
                runtime,
                services,
                TestContext.CancellationToken);

            Assert.AreEqual(DebugResumeAction.Continue, action);
        },
        buildConfiguration: static config =>
        {
            IServiceSelectionKey<IDebugFrontend> frontendKey = config.ServiceProvider.GetRequiredService<IServiceSelectionKey<IDebugFrontend>>();
            config.Ignore(frontendKey.Key);
        });

    [TestMethod]
    public Task Test_ConcurrentDecidedPauses_AreFifoAndBreakpointDeletionDoesNotRevokeQueuedAsync() => TestWithDIAsync(
        assertion: async services =>
        {
            IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();
            ControlledFrontend frontend = GetFrontend<ControlledFrontend>(services);
            breakpoints.Add(".*");

            ValueTask<DebugResumeAction> first = EvaluateAsync(debugger, runtime, services, "first", TestContext.CancellationToken);
            ControlledPause firstPause = await frontend.WaitForPauseAsync(1, TestContext.CancellationToken);
            ValueTask<DebugResumeAction> second = EvaluateAsync(debugger, runtime, services, "second", TestContext.CancellationToken);
            ValueTask<DebugResumeAction> third = EvaluateAsync(debugger, runtime, services, "third", TestContext.CancellationToken);

            Assert.AreEqual(1, frontend.PauseCount);
            breakpoints.Clear();
            firstPause.Resume(DebugResumeAction.Continue);
            Assert.AreEqual(DebugResumeAction.Continue, await first);

            ControlledPause secondPause = await frontend.WaitForPauseAsync(2, TestContext.CancellationToken);
            Assert.AreEqual("cyborg.tests.probe.v1 name=second", secondPause.Identity);
            secondPause.Resume(DebugResumeAction.Continue);
            Assert.AreEqual(DebugResumeAction.Continue, await second);

            ControlledPause thirdPause = await frontend.WaitForPauseAsync(3, TestContext.CancellationToken);
            Assert.AreEqual("cyborg.tests.probe.v1 name=third", thirdPause.Identity);
            thirdPause.Resume(DebugResumeAction.Continue);
            Assert.AreEqual(DebugResumeAction.Continue, await third);
            Assert.AreSequenceEqual(
                [
                    "cyborg.tests.probe.v1 name=first",
                    "cyborg.tests.probe.v1 name=second",
                    "cyborg.tests.probe.v1 name=third",
                ],
                frontend.Identities);
        },
        configureServices: static services => services.AddSingleton<IDebugFrontend, ControlledFrontend>());

    [TestMethod]
    public Task Test_Detach_InvalidatesQueuedAndFuturePausesAsync() => TestWithDIAsync(
        assertion: async services =>
        {
            IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();
            ControlledFrontend frontend = GetFrontend<ControlledFrontend>(services);
            breakpoints.Add(".*");

            ValueTask<DebugResumeAction> first = EvaluateAsync(debugger, runtime, services, "first", TestContext.CancellationToken);
            ControlledPause firstPause = await frontend.WaitForPauseAsync(1, TestContext.CancellationToken);
            ValueTask<DebugResumeAction> second = EvaluateAsync(debugger, runtime, services, "second", TestContext.CancellationToken);
            Assert.AreEqual(1, frontend.PauseCount);

            firstPause.Resume(DebugResumeAction.Detach);
            Assert.AreEqual(DebugResumeAction.Continue, await first);
            Assert.AreEqual(DebugResumeAction.Continue, await second);
            Assert.AreEqual(1, frontend.PauseCount);
            Assert.AreEqual(0, breakpoints.Count);

            DebugResumeAction future = await EvaluateAsync(debugger, runtime, services, "future", TestContext.CancellationToken);
            Assert.AreEqual(DebugResumeAction.Continue, future);
            Assert.AreEqual(1, frontend.PauseCount);
        },
        configureServices: static services => services.AddSingleton<IDebugFrontend, ControlledFrontend>());

    [TestMethod]
    public Task Test_QueuedPauseCancellation_RemovesQueueEntryAndDoesNotBlockFollowingPauseAsync() => TestWithDIAsync(
        assertion: async services =>
        {
            IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();
            ControlledFrontend frontend = GetFrontend<ControlledFrontend>(services);
            breakpoints.Add(".*");
            using CancellationTokenSource queuedCancellation = new();

            ValueTask<DebugResumeAction> first = EvaluateAsync(debugger, runtime, services, "first", TestContext.CancellationToken);
            ControlledPause firstPause = await frontend.WaitForPauseAsync(1, TestContext.CancellationToken);
            ValueTask<DebugResumeAction> canceled = EvaluateAsync(debugger, runtime, services, "canceled", queuedCancellation.Token);
            ValueTask<DebugResumeAction> third = EvaluateAsync(debugger, runtime, services, "third", TestContext.CancellationToken);

            await queuedCancellation.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await canceled);
            Assert.AreEqual(1, frontend.PauseCount);

            firstPause.Resume(DebugResumeAction.Continue);
            Assert.AreEqual(DebugResumeAction.Continue, await first);
            ControlledPause thirdPause = await frontend.WaitForPauseAsync(2, TestContext.CancellationToken);
            Assert.AreEqual("cyborg.tests.probe.v1 name=third", thirdPause.Identity);
            thirdPause.Resume(DebugResumeAction.Continue);
            Assert.AreEqual(DebugResumeAction.Continue, await third);
            Assert.AreEqual(2, frontend.PauseCount);
        },
        configureServices: static services => services.AddSingleton<IDebugFrontend, ControlledFrontend>());

    [TestMethod]
    public Task Test_EvaluatePreExecutionAsync_WhenSpecifiedFrontendIsNotFound_ThrowsAsync() => TestWithDIAsync(
        assertion: async services =>
        {
            IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();
            breakpoints.Add("probe");

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await debugger.EvaluatePreExecutionAsync(
                    ProbeModule.ModuleId,
                    ValidationResult.Valid(new ProbeModule()),
                    runtime,
                    services,
                    TestContext.CancellationToken));
        },
        buildConfiguration: static config =>
        {
            IServiceSelectionKey<IDebugFrontend> frontendKey = config.ServiceProvider.GetRequiredService<IServiceSelectionKey<IDebugFrontend>>();
            config.AddDictionary(dict => dict.AddEntry(frontendKey.Key, "does-not-exist"));
        });

    private static ValueTask<DebugResumeAction> EvaluateAsync(
        IWorkflowDebugger debugger,
        IModuleRuntime runtime,
        IServiceProvider services,
        string name,
        CancellationToken cancellationToken) =>
        debugger.EvaluatePreExecutionAsync(
            ProbeModule.ModuleId,
            ValidationResult.Valid(new ProbeModule { Name = name }),
            runtime,
            services,
            cancellationToken);

    private static TFrontend GetFrontend<TFrontend>(IServiceProvider services) where TFrontend : class, IDebugFrontend
    {
        IDebugFrontend frontend = services.GetRequiredService<IDebugFrontend>();
        Assert.IsInstanceOfType<TFrontend>(frontend);
        return (TFrontend)frontend;
    }

    private sealed class TestDebugBranchControl : IDebugBranchControl
    {
        public bool IsStepping { get; private set; }

        public void Step() => IsStepping = true;

        public void Continue() => IsStepping = false;
    }

    private sealed record ProbeModule : ModuleBase, IModule
    {
        public static string ModuleId => "cyborg.tests.probe.v1";
    }

    private sealed class ScriptedFrontend(DebugResumeAction action) : IDebugFrontend
    {
        public string Key => "test";

        public int PauseCount { get; private set; }

        public string? LastIdentity { get; private set; }

        public IModule? LastModule { get; private set; }

        public bool LastIsValid { get; private set; }

        public IReadOnlyList<ValidationError> LastValidationErrors { get; private set; } = [];

        public ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PauseCount++;
            LastIdentity = context.GetModuleIdentity();
            LastModule = context.ValidationResult.Module;
            LastIsValid = context.ValidationResult.IsValid;
            LastValidationErrors = context.ValidationResult.Errors;
            return ValueTask.FromResult(action);
        }
    }

    private sealed class ScriptedSequenceFrontend(IReadOnlyList<DebugResumeAction> actions) : IDebugFrontend
    {
        public string Key => "test";

        public int PauseCount { get; private set; }

        public ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = PauseCount++;
            Assert.IsLessThan(actions.Count, index);
            return ValueTask.FromResult(actions[index]);
        }
    }

    private sealed class ControlledFrontend : IDebugFrontend
    {
        private readonly object _lock = new();
        private readonly List<ControlledPause> _pauses = [];
        private readonly SemaphoreSlim _entered = new(initialCount: 0);

        public string Key => "test";

        public int PauseCount
        {
            get
            {
                lock (_lock)
                {
                    return _pauses.Count;
                }
            }
        }

        public IReadOnlyList<string> Identities
        {
            get
            {
                lock (_lock)
                {
                    return _pauses.Select(static pause => pause.Identity).ToArray();
                }
            }
        }

        public async ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
        {
            ControlledPause pause = new(context.GetModuleIdentity());
            lock (_lock)
            {
                _pauses.Add(pause);
            }
            _entered.Release();
            return await pause.WaitForResumeAsync(cancellationToken);
        }

        public async Task<ControlledPause> WaitForPauseAsync(int expectedCount, CancellationToken cancellationToken)
        {
            while (true)
            {
                lock (_lock)
                {
                    if (_pauses.Count >= expectedCount)
                    {
                        return _pauses[expectedCount - 1];
                    }
                }
                await _entered.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed class ControlledPause(string identity)
    {
        private readonly TaskCompletionSource<DebugResumeAction> _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Identity { get; } = identity;

        public void Resume(DebugResumeAction action) => _resume.TrySetResult(action);

        public async ValueTask<DebugResumeAction> WaitForResumeAsync(CancellationToken cancellationToken) =>
            await _resume.Task.WaitAsync(cancellationToken);
    }
}
