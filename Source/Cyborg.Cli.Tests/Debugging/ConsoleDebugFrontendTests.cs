using Cyborg.Cli.Debugging;
using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

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
    public async Task PauseAsync_ContinueAlias_ResumesExecutionAsync()
    {
        (DebugResumeAction action, _) = await RunReplAsync("c\n");
        Assert.AreEqual(DebugResumeAction.Continue, action);
    }

    [TestMethod]
    public async Task PauseAsync_Help_UsesGeneratedCommandHelpAsync()
    {
        (DebugResumeAction action, string output) = await RunReplAsync("help\ncontinue\n");

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Commands:", output);
        Assert.Contains("break", output);
    }

    [TestMethod]
    public async Task PauseAsync_RepeatedPauses_ReuseDispatcherAsync()
    {
        (DebugResumeAction action, _) = await RunReplWithRegistryAsync(
            "continue\ncontinue\n",
            new BreakpointRegistry(),
            pauseCount: 2);

        Assert.AreEqual(DebugResumeAction.Continue, action);
    }

    [TestMethod]
    public async Task PauseAsync_Inspect_PrintsStateAndStaysUntilContinueAsync()
    {
        (DebugResumeAction action, string output) = await RunReplAsync("inspect\ncontinue\n");
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("cyborg.tests.probe.v1", output);
    }

    [TestMethod]
    public async Task PauseAsync_CafNestedAliases_RouteCommandsAsync()
    {
        (DebugResumeAction action, string output, IBreakpointRegistry breakpoints) =
            await RunReplWithRegistryAsync(
                "b at other\nb ls\nb rm 1\ni\ns\n",
                new BreakpointRegistry());

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint 1 set: other", output);
        Assert.Contains("Removed breakpoint 1.", output);
        Assert.Contains("cyborg.tests.probe.v1", output);
        Assert.Contains(
            static breakpoint => breakpoint.Expression == ".*" && breakpoint.IsOneShot,
            breakpoints.List());
    }

    [TestMethod]
    public async Task PauseAsync_UnknownCommand_StaysInReplAsync()
    {
        (DebugResumeAction action, _) = await RunReplAsync(
            "not-a-command\nresume\n");

        Assert.AreEqual(DebugResumeAction.Continue, action);
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
    public async Task PauseAsync_BreakRm_InvalidId_StaysInReplAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("seed");

        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) =
            await RunReplWithRegistryAsync(
                "break rm nope\ncontinue\n",
                registry);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(1, breakpoints.Count);
    }

    [TestMethod]
    public async Task PauseAsync_BreakAt_QuotedExpressionPreservesSpacesAsync()
    {
        (DebugResumeAction action, string output, IBreakpointRegistry breakpoints) =
            await RunReplWithRegistryAsync(
                "break at \"other module\"\ncontinue\n",
                new BreakpointRegistry());

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint 1 set: other module", output);
        Assert.Contains(
            static breakpoint => breakpoint.Expression == "other module",
            breakpoints.List());
    }

    [TestMethod]
    public async Task PauseAsync_BreakAt_UnquotedExpressionJoinsRemainingTokensAsync()
    {
        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) =
            await RunReplWithRegistryAsync(
                "break at other module\ncontinue\n",
                new BreakpointRegistry());

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(
            static breakpoint => breakpoint.Expression == "other module",
            breakpoints.List());
    }

    [TestMethod]
    public async Task PauseAsync_Step_AddsOneShotWildcardAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("probe");
        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) =
            await RunReplWithRegistryAsync("step\n", registry);
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(
            static breakpoint => breakpoint.Expression == ".*" && breakpoint.IsOneShot,
            breakpoints.List());
    }

    [TestMethod]
    public async Task PauseAsync_Detach_ClearsBreakpointsAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("probe");
        registry.Add("other");
        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) =
            await RunReplWithRegistryAsync("detach\n", registry);
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
        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) =
            await RunReplWithRegistryAsync(string.Empty, new BreakpointRegistry());
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(0, breakpoints.Count);
    }

    private async Task<(DebugResumeAction Action, string Output)> RunReplAsync(
        string script,
        string[]? seedBreakpoints = null)
    {
        BreakpointRegistry registry = new();
        if (seedBreakpoints is not null)
        {
            foreach (string expression in seedBreakpoints)
            {
                registry.Add(expression);
            }
        }

        (DebugResumeAction action, string output, _) =
            await RunReplWithRegistryAsync(script, registry);
        return (action, output);
    }

    private async Task<(
        DebugResumeAction Action,
        string Output,
        IBreakpointRegistry Breakpoints)> RunReplWithRegistryAsync(
            string script,
            BreakpointRegistry registry,
            int pauseCount = 1)
    {
        using StringReader input = new(script);
        StringBuilder outputBuilder = new();
        using StringWriter output = new(outputBuilder);
        TextDebugReplIo io = new(input, output);
        DebugCommandDispatcher dispatcher = new(io);
        ConsoleDebugFrontend frontend = new(io, dispatcher);

        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        GlobalRuntimeEnvironment environment = new(JsonNamingPolicy.SnakeCaseLower);
        RootModuleRuntime runtime = new(environment, loggerFactory);
        ProbeModule module = new() { Name = "probe" };

        DebugPauseContextStub context = new(
            module,
            ProbeModule.ModuleId,
            runtime,
            registry,
            requestStep: () => registry.Add(".*", isOneShot: true),
            detach: registry.Clear);

        DebugResumeAction action = default;
        for (int index = 0; index < pauseCount; index++)
        {
            action = await frontend.PauseAsync(
                context,
                TestContext.CancellationToken);
        }

        return (action, outputBuilder.ToString(), registry);
    }

    private sealed class DebugPauseContextStub(
        IModule module,
        string moduleId,
        IModuleRuntime runtime,
        IBreakpointRegistry breakpoints,
        Action requestStep,
        Action detach) : IDebugPauseContext
    {
        public IModule Module { get; } = module;

        public string ModuleId { get; } = moduleId;

        public string ModuleIdentity { get; } =
            global::Cyborg.Core.Modules.Debugging.ModuleIdentity.Format(module, moduleId);

        public IModuleRuntime Runtime { get; } = runtime;

        public IBreakpointRegistry Breakpoints { get; } = breakpoints;

        public ValueTask<string> InspectAsync(CancellationToken cancellationToken)
        {
            if (Module is IModuleDescriptor descriptor)
            {
                return ModuleDescription.ToTextAsync(descriptor, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ModuleIdentity);
        }

        public void RequestStep() => requestStep();

        public void Detach() => detach();
    }
}

[GeneratedModuleValidation]
internal sealed partial record ProbeModule : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.tests.probe.v1";
}
