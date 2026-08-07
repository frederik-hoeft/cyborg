using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Descriptors.Writers;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class WorkflowDebuggerTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task EvaluatePreExecutionAsync_WhenDisabled_ReturnsContinueWithoutFrontendAsync()
    {
        BreakpointRegistry registry = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        WorkflowDebugger debugger = CreateDebugger(registry, loggerFactory);
        GlobalRuntimeEnvironment env = new(JsonNamingPolicy.SnakeCaseLower);
        RootModuleRuntime runtime = new(env, loggerFactory);
        ProbeModule module = new();

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(module, ProbeModule.ModuleId, runtime, TestContext.CancellationToken);
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.IsFalse(debugger.IsEnabled);
    }

    [TestMethod]
    public async Task EvaluatePreExecutionAsync_WhenBreakpointMatches_InvokesFrontendAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("probe");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        WorkflowDebugger debugger = CreateDebugger(registry, loggerFactory);
        ScriptedFrontend frontend = new(DebugResumeAction.Continue);
        debugger.Frontend = frontend;

        GlobalRuntimeEnvironment env = new(JsonNamingPolicy.SnakeCaseLower);
        RootModuleRuntime runtime = new(env, loggerFactory);
        ProbeModule module = new() { Name = "probe-step" };

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(module, ProbeModule.ModuleId, runtime, TestContext.CancellationToken);
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
        WorkflowDebugger debugger = CreateDebugger(registry, loggerFactory);
        debugger.Frontend = new ScriptedFrontend(DebugResumeAction.Cancel);

        GlobalRuntimeEnvironment env = new(JsonNamingPolicy.SnakeCaseLower);
        RootModuleRuntime runtime = new(env, loggerFactory);

        DebugResumeAction action = await debugger.EvaluatePreExecutionAsync(new ProbeModule(), ProbeModule.ModuleId, runtime, TestContext.CancellationToken);
        Assert.AreEqual(DebugResumeAction.Cancel, action);
    }

    [TestMethod]
    public async Task RequestStep_RegistersOneShotWildcardAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("first");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        WorkflowDebugger debugger = CreateDebugger(registry, loggerFactory);
        StepThenContinueFrontend frontend = new();
        debugger.Frontend = frontend;

        GlobalRuntimeEnvironment env = new(JsonNamingPolicy.SnakeCaseLower);
        RootModuleRuntime runtime = new(env, loggerFactory);
        ProbeModule module = new() { Name = "first" };

        await debugger.EvaluatePreExecutionAsync(module, ProbeModule.ModuleId, runtime, TestContext.CancellationToken);

        IReadOnlyList<BreakpointExpression> list = registry.List();
        Assert.Contains(static b => b.Expression == WorkflowDebugger.STEP_EXPRESSION && b.IsOneShot, list);
    }

    private static WorkflowDebugger CreateDebugger(BreakpointRegistry registry, ILoggerFactory loggerFactory)
    {
        DefaultModuleDescriptionSerializerRegistry serializers = new([TextModuleDescriptionSerializer.Instance]);
        return new WorkflowDebugger(registry, serializers, loggerFactory);
    }

    private sealed record ProbeModule : ModuleBase, IModule
    {
        public static string ModuleId => "cyborg.tests.probe.v1";
    }

    private sealed class ScriptedFrontend(DebugResumeAction action) : IDebugFrontend
    {
        public int PauseCount { get; private set; }

        public string? LastIdentity { get; private set; }

        public ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
        {
            PauseCount++;
            LastIdentity = context.ModuleIdentity;
            return ValueTask.FromResult(action);
        }
    }

    private sealed class StepThenContinueFrontend : IDebugFrontend
    {
        public ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
        {
            context.RequestStep();
            return ValueTask.FromResult(DebugResumeAction.Continue);
        }
    }
}
