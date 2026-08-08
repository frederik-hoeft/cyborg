using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Services.Default;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class WorkflowDebuggerTests
{
    private static readonly IServiceProvider s_services = new EmptyServiceProvider();

    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task EvaluatePreExecutionAsync_WhenDisabled_ReturnsContinueWithoutFrontendAsync()
    {
        BreakpointRegistry registry = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        WorkflowDebugger debugger = CreateDebugger(registry, loggerFactory, frontend: null);
        RootModuleRuntime runtime = new(new GlobalRuntimeEnvironment(JsonNamingPolicy.SnakeCaseLower), loggerFactory);

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, ValidationResult.Valid(new ProbeModule()), runtime, s_services, TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.IsFalse(debugger.IsEnabled);
    }

    [TestMethod]
    public async Task EvaluatePreExecutionAsync_WhenBreakpointMatches_InvokesFrontendAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("probe");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        ScriptedFrontend frontend = new(DebugResumeAction.Continue);
        WorkflowDebugger debugger = CreateDebugger(registry, loggerFactory, frontend);
        RootModuleRuntime runtime = new(new GlobalRuntimeEnvironment(JsonNamingPolicy.SnakeCaseLower), loggerFactory);
        ProbeModule module = new() { Name = "probe-step" };

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, ValidationResult.Valid(module), runtime, s_services, TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(1, frontend.PauseCount);
        Assert.AreEqual("cyborg.tests.probe.v1 name=probe-step", frontend.LastIdentity);
    }

    [TestMethod]
    public async Task EvaluatePreExecutionAsync_CancelFromFrontend_PropagatesAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add(".*");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        WorkflowDebugger debugger = CreateDebugger(registry, loggerFactory, new ScriptedFrontend(DebugResumeAction.Cancel));
        RootModuleRuntime runtime = new(new GlobalRuntimeEnvironment(JsonNamingPolicy.SnakeCaseLower), loggerFactory);

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, ValidationResult.Valid(new ProbeModule()), runtime, s_services, TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Cancel, action);
    }

    [TestMethod]
    public async Task RequestStep_RegistersOneShotWildcardAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("first");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        WorkflowDebugger debugger = CreateDebugger(registry, loggerFactory, new StepThenContinueFrontend());
        RootModuleRuntime runtime = new(new GlobalRuntimeEnvironment(JsonNamingPolicy.SnakeCaseLower), loggerFactory);
        ProbeModule module = new() { Name = "first" };

        await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, ValidationResult.Valid(module), runtime, s_services, TestContext.CancellationToken);

        IReadOnlyList<BreakpointExpression> breakpoints = registry.ToList();
        Assert.Contains(static breakpoint => breakpoint.Expression == WorkflowDebugger.STEP_EXPRESSION && breakpoint.IsOneShot, breakpoints);
    }

    [TestMethod]
    public async Task EvaluatePreExecutionAsync_InvalidResult_PassesPreparedModuleAndErrorsToFrontendAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("probe");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        ScriptedFrontend frontend = new(DebugResumeAction.Continue);
        WorkflowDebugger debugger = CreateDebugger(registry, loggerFactory, frontend);
        RootModuleRuntime runtime = new(new GlobalRuntimeEnvironment(JsonNamingPolicy.SnakeCaseLower), loggerFactory);
        ProbeModule module = new() { Name = "probe-invalid" };
        ValidationError error = new(nameof(ProbeModule.Name), "test-rule", "The probe is invalid.");
        IValidationResult<ProbeModule> validationResult = ValidationResult.Invalid(module, [error]);

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, validationResult, runtime, s_services, TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.IsFalse(frontend.LastIsValid);
        Assert.AreEqual(module, frontend.LastModule);
        Assert.AreSequenceEqual([error], frontend.LastValidationErrors);
    }

    [TestMethod]
    public async Task EvaluatePreExecutionAsync_WhenFrontendConfigurationIsInvalid_ThrowsAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("probe");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        WorkflowDebugger debugger = CreateDebugger(registry, loggerFactory, frontend: null);
        RootModuleRuntime runtime = new(new GlobalRuntimeEnvironment(JsonNamingPolicy.SnakeCaseLower), loggerFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await debugger.EvaluatePreExecutionAsync(ProbeModule.ModuleId, ValidationResult.Valid(new ProbeModule()), runtime, s_services, TestContext.CancellationToken));
    }

    private static WorkflowDebugger CreateDebugger(BreakpointRegistry registry, ILoggerFactory loggerFactory, IDebugFrontend? frontend) =>
        new(registry, loggerFactory, new TestDefaultFrontend(frontend));

    private sealed record ProbeModule : ModuleBase, IModule
    {
        public static string ModuleId => "cyborg.tests.probe.v1";
    }

    private sealed class TestDefaultFrontend(IDebugFrontend? frontend) : IDefault<IDebugFrontend>
    {
        public IDebugFrontend? GetDefault() => frontend;

        public IDebugFrontend GetRequiredDefault() => frontend ?? throw new InvalidOperationException("No frontend configured.");
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

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IServiceProvider) ? this : null;
    }
}
