using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Services.Default;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class WorkflowDebuggerTests : CyborgCoreTestBase
{
    protected override void BuildConfiguration(IConfigurationBuilder configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        IServiceSelectionKey<IDebugFrontend> frontendKey = configuration.ServiceProvider.GetRequiredService<IServiceSelectionKey<IDebugFrontend>>();
        // use ScriptedFrontend as the default frontend for testing
        configuration.AddDictionary(dict => dict.AddEntry(frontendKey.Key, "test"));
    }

    [TestMethod]
    public Task Test_EvaluatePreExecutionAsync_WhenDisabled_ReturnsContinueWithoutFrontendAsync() => TestWithDIAsync(async services =>
    {
        IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, ValidationResult.Valid(new ProbeModule()), runtime, services, TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.IsFalse(debugger.IsEnabled);
    });

    [TestMethod]
    public Task Test_EvaluatePreExecutionAsync_WhenBreakpointMatches_InvokesFrontendAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();

        breakpoints.Add("probe");
        ProbeModule module = new() { Name = "probe-step" };
        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, ValidationResult.Valid(module), runtime, services, TestContext.CancellationToken);

        IDebugFrontend debugFrontend = services.GetRequiredService<IDebugFrontend>();
        Assert.IsInstanceOfType<ScriptedFrontend>(debugFrontend);
        ScriptedFrontend frontend = (ScriptedFrontend)debugFrontend;
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(1, frontend.PauseCount);
        Assert.AreEqual("cyborg.tests.probe.v1 name=probe-step", frontend.LastIdentity);
    }, static services => services.AddSingleton<IDebugFrontend>(new ScriptedFrontend(DebugResumeAction.Continue)));

    [TestMethod]
    public Task Test_EvaluatePreExecutionAsync_CancelFromFrontend_PropagatesAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();

        breakpoints.Add(".*");

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(
            ProbeModule.ModuleId,
            ValidationResult.Valid(new ProbeModule()),
            runtime,
            services,
            TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Cancel, action);
    }, static services => services.AddSingleton<IDebugFrontend>(new ScriptedFrontend(DebugResumeAction.Cancel)));

    [TestMethod]
    public Task Test_RequestStep_RegistersOneShotWildcardAsync() => TestWithDIAsync(
        assertion: async services =>
        {
            IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();

            breakpoints.Add("first");
            ProbeModule module = new() { Name = "first" };

            await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, ValidationResult.Valid(module), runtime, services, TestContext.CancellationToken);

            IReadOnlyList<BreakpointExpression> registeredBreakpoints = breakpoints.ToList();
            Assert.Contains(static breakpoint => breakpoint.Expression == WorkflowDebugger.STEP_EXPRESSION && breakpoint.IsOneShot, registeredBreakpoints);
        },
        configureServices: static services => services.AddSingleton<IDebugFrontend, StepThenContinueFrontend>(),
        buildConfiguration: static config =>
        {
            IServiceSelectionKey<IDebugFrontend> frontendKey = config.ServiceProvider.GetRequiredService<IServiceSelectionKey<IDebugFrontend>>();

            config.AddDictionary(dict => dict.AddEntry(frontendKey.Key, "test-step"));
        });

    [TestMethod]
    public Task Test_EvaluatePreExecutionAsync_InvalidResult_PassesPreparedModuleAndErrorsToFrontendAsync() => TestWithDIAsync(
        assertion: async services =>
        {
            IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();

            breakpoints.Add("probe");

            ProbeModule module = new() { Name = "probe-invalid" };
            ValidationError error = new(nameof(ProbeModule.Name), "test-rule", "The probe is invalid.");
            IValidationResult<ProbeModule> validationResult = ValidationResult.Invalid(module, [error]);

            DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, validationResult, runtime, services, TestContext.CancellationToken);

            IDebugFrontend debugFrontend = services.GetRequiredService<IDebugFrontend>();
            Assert.IsInstanceOfType<ScriptedFrontend>(debugFrontend);
            ScriptedFrontend frontend = (ScriptedFrontend)debugFrontend;

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

            DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, ValidationResult.Valid(new ProbeModule()), runtime, services, TestContext.CancellationToken);

            Assert.AreEqual(DebugResumeAction.Continue, action);
        },
        buildConfiguration: static config =>
        {
            IServiceSelectionKey<IDebugFrontend> frontendKey = config.ServiceProvider.GetRequiredService<IServiceSelectionKey<IDebugFrontend>>();
            config.Ignore(frontendKey.Key);
        });

    [TestMethod]
    public Task Test_EvaluatePreExecutionAsync_WhenSpecifiedFrontendIsNotFound_ThrowsAsync() => TestWithDIAsync(
        assertion: async services =>
        {
            IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IWorkflowDebugger debugger = services.GetRequiredService<IWorkflowDebugger>();

            breakpoints.Add("probe");

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, ValidationResult.Valid(new ProbeModule()), runtime, services, TestContext.CancellationToken));
        },
        buildConfiguration: static config =>
        {
            IServiceSelectionKey<IDebugFrontend> frontendKey = config.ServiceProvider.GetRequiredService<IServiceSelectionKey<IDebugFrontend>>();

            config.AddDictionary(dict => dict.AddEntry(frontendKey.Key, "does-not-exist"));
        });

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

    private sealed class StepThenContinueFrontend : IDebugFrontend
    {
        public string Key => "test-step";

        public ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.RequestStep();
            return ValueTask.FromResult(DebugResumeAction.Continue);
        }
    }
}
