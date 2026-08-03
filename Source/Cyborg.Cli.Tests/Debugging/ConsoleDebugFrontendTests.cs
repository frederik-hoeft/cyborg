using Cyborg.Cli.Debugging;
using Cyborg.Cli.Debugging.Commands;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using Cyborg.Core.Aot.Modules.Validation;

namespace Cyborg.Cli.Tests.Debugging;

[TestClass]
public sealed class ConsoleDebugFrontendTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task PauseAsync_Continue_ResumesExecutionAsync()
    {
        (DebugResumeAction action, string output) = await RunReplAsync("continue\n");
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint hit:", output);
    }

    [TestMethod]
    public async Task PauseAsync_Inspect_PrintsStateAndStaysUntilContinueAsync()
    {
        (DebugResumeAction action, string output) = await RunReplAsync("inspect\ncontinue\n");
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("cyborg.tests.probe.v1", output);
    }

    [TestMethod]
    public async Task PauseAsync_BreakLsAndRm_ManageBreakpointsAsync()
    {
        (DebugResumeAction action, string output) = await RunReplAsync(
            "break at other\nbreak ls\nbreak rm 2\nbreak ls\ncontinue\n",
            seedBreakpoints: ["seed"]);
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint 2 set: other", output);
        Assert.Contains("Removed breakpoint 2.", output);
    }

    [TestMethod]
    public async Task PauseAsync_Step_AddsOneShotWildcardAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("probe");
        (DebugResumeAction action, string output, IBreakpointRegistry breakpoints) = await RunReplWithRegistryAsync("step\n", registry);
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(static b => b.Expression == ".*" && b.IsOneShot, breakpoints.List());
    }

    [TestMethod]
    public async Task PauseAsync_Detach_ClearsBreakpointsAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("probe");
        registry.Add("other");
        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) = await RunReplWithRegistryAsync("detach\n", registry);
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(0, breakpoints.Count);
    }

    [TestMethod]
    public async Task PauseAsync_Cancel_ReturnsCancelAsync()
    {
        (DebugResumeAction action, _) = await RunReplAsync("cancel\n");
        Assert.AreEqual(DebugResumeAction.Cancel, action);
    }

    [TestMethod]
    public async Task PauseAsync_Eof_DetachesAndContinuesAsync()
    {
        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) = await RunReplWithRegistryAsync("", new BreakpointRegistry());
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(0, breakpoints.Count);
    }

    private async Task<(DebugResumeAction Action, string Output)> RunReplAsync(string script, string[]? seedBreakpoints = null)
    {
        BreakpointRegistry registry = new();
        if (seedBreakpoints is not null)
        {
            foreach (string expression in seedBreakpoints)
            {
                registry.Add(expression);
            }
        }
        (DebugResumeAction action, string output, _) = await RunReplWithRegistryAsync(script, registry);
        return (action, output);
    }

    private async Task<(DebugResumeAction Action, string Output, IBreakpointRegistry Breakpoints)> RunReplWithRegistryAsync(string script, BreakpointRegistry registry)
    {
        using StringReader input = new(script);
        StringBuilder outputBuilder = new();
        using StringWriter output = new(outputBuilder);
        TextDebugReplIo io = new(input, output);

        IDebugReplCommand[] commands =
        [
            new ContinueCommand(),
            new DetachCommand(),
            new StepCommand(),
            new InspectCommand(io),
            new CancelCommand(),
            new BreakCommand(io),
        ];

        ConsoleDebugFrontend frontend = new(io, commands);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        GlobalRuntimeEnvironment env = new(JsonNamingPolicy.SnakeCaseLower);
        RootModuleRuntime runtime = new(env, loggerFactory);
        ProbeModule module = new() { Name = "probe" };

        DebugPauseContextStub context = new(
            module,
            ProbeModule.ModuleId,
            runtime,
            registry,
            requestStep: () => registry.Add(".*", isOneShot: true),
            detach: registry.Clear);

        DebugResumeAction action = await frontend.PauseAsync(context, TestContext.CancellationToken);
        return (action, outputBuilder.ToString(), registry);
    }

    private sealed class DebugPauseContextStub(IModule module, string moduleId, IModuleRuntime runtime, IBreakpointRegistry breakpoints, Action requestStep, Action detach) : IDebugPauseContext
    {
        public IModule Module { get; } = module;

        public string ModuleId { get; } = moduleId;

        public string ModuleIdentity { get; } = Core.Modules.Debugging.ModuleIdentity.Format(module, moduleId);

        public IModuleRuntime Runtime { get; } = runtime;

        public IBreakpointRegistry Breakpoints { get; } = breakpoints;

        public string Inspect() => Module is IInspectable inspectable ? inspectable.Inspect() : ModuleIdentity;

        public void RequestStep() => requestStep();

        public void Detach() => detach();
    }
}

[GeneratedModuleValidation]
internal sealed partial record ProbeModule : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.tests.probe.v1";
}
