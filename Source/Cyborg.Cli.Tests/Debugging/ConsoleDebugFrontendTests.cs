using Cyborg.Cli;
using Cyborg.Cli.Debugging;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Services.Default;
using Cyborg.TestModules.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Cyborg.Cli.Tests.Debugging;

[TestClass]
public sealed class ConsoleDebugFrontendTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void DefaultFrontend_ResolvesConsoleFrontend()
    {
        using DefaultServiceProvider services = new();
        IDefault<IDebugFrontend> defaultFrontend = services.GetRequiredService<IDefault<IDebugFrontend>>();

        Assert.AreEqual("console", defaultFrontend.GetRequiredDefault().Key);
    }

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
        Assert.DoesNotContain("run", output);
    }

    [TestMethod]
    public async Task PauseAsync_RepeatedPauses_ReuseDispatcherAsync()
    {
        (DebugResumeAction action, _, _) = await RunReplWithRegistryAsync(
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

    [TestMethod]
    public async Task PauseAsync_UsesPromptAwareIoAndSemanticOutputKindsAsync()
    {
        BreakpointRegistry registry = new();
        RecordingDebugReplIo io = new(["break at other", "continue"]);
        using DefaultServiceProvider services = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        GlobalRuntimeEnvironment environment = new(JsonNamingPolicy.SnakeCaseLower);
        RootModuleRuntime runtime = new(environment, loggerFactory);
        ProbeModule module = new() { Name = "probe" };
        DebugPauseContextStub context = new(module, ProbeModule.ModuleId, runtime, services, registry, requestStep: () => registry.Add(".*", isOneShot: true), detach: registry.Clear);

        DebugResumeAction action = await CreateFrontend(io).PauseAsync(context, TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(static prompt => prompt == "(cyborg-dbg) ", io.Prompts);
        Assert.Contains(static write => write.Message == "Breakpoint 1 set: other" && write.Kind == DebugReplOutputKind.Success, io.Writes);
        Assert.Contains(static write => write.Message.StartsWith("Breakpoint hit:", StringComparison.Ordinal) && write.Kind == DebugReplOutputKind.Status, io.Writes);
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

    private async Task<(DebugResumeAction Action, string Output, IBreakpointRegistry Breakpoints)> RunReplWithRegistryAsync(string script, BreakpointRegistry registry, int pauseCount = 1)
    {
        using StringReader input = new(script);
        StringBuilder outputBuilder = new();
        using StringWriter output = new(outputBuilder);
        TextDebugReplIo io = new(input, output);
        using DefaultServiceProvider services = new();
        ConsoleDebugFrontend frontend = CreateFrontend(io);

        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        GlobalRuntimeEnvironment environment = new(JsonNamingPolicy.SnakeCaseLower);
        RootModuleRuntime runtime = new(environment, loggerFactory);
        ProbeModule module = new() { Name = "probe" };

        DebugPauseContextStub context = new(module, ProbeModule.ModuleId, runtime, services, registry, requestStep: () => registry.Add(".*", isOneShot: true), detach: registry.Clear);

        DebugResumeAction action = default;
        for (int index = 0; index < pauseCount; index++)
        {
            action = await frontend.PauseAsync(context, TestContext.CancellationToken);
        }

        return (action, outputBuilder.ToString(), registry);
    }

    private static ConsoleDebugFrontend CreateFrontend(IDebugReplIo io) => new(io, new DebugCommandDispatcher(io, new CafDebugCommandRouter()));

    private sealed class DebugPauseContextStub(
        IModule module, string moduleId, IModuleRuntime runtime, IServiceProvider services, IBreakpointRegistry breakpoints, Action requestStep, Action detach) : IDebugPauseContext
    {
        public IModule Module { get; } = module;

        public string ModuleId { get; } = moduleId;

        public string ModuleIdentity { get; } = global::Cyborg.Core.Modules.Debugging.ModuleIdentity.Format(module, moduleId);

        public IModuleRuntime Runtime { get; } = runtime;

        public IServiceProvider Services { get; } = services;

        public IBreakpointRegistry Breakpoints { get; } = breakpoints;

        public void RequestStep() => requestStep();

        public void Detach() => detach();
    }

    private sealed class RecordingDebugReplIo(IEnumerable<string?> input) : IDebugReplIo
    {
        private readonly Queue<string?> _input = new(input);

        public List<string> Prompts { get; } = [];

        public List<(string Message, DebugReplOutputKind Kind)> Writes { get; } = [];

        public void Write(string message, DebugReplOutputKind kind = DebugReplOutputKind.Text) => Writes.Add((message, kind));

        public void WriteLine(string message, DebugReplOutputKind kind = DebugReplOutputKind.Text) => Writes.Add((message, kind));

        public ValueTask<string?> ReadLineAsync(string prompt, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Prompts.Add(prompt);
            return new(_input.Count == 0 ? null : _input.Dequeue());
        }
    }
}
