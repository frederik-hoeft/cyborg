using Cyborg.Cli.Debugging;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Services.Default;
using Cyborg.TestModules.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using DebugResumeContext = (Cyborg.Core.Modules.Debugging.DebugResumeAction Action, string Output, Cyborg.Core.Modules.Debugging.Breakpoints.IBreakpointRegistry Breakpoints);

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
    public async Task Test_PauseAsync_Continue_ResumesExecutionAsync()
    {
        (DebugResumeAction action, string output) = await RunReplAsync("continue\n");
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint hit:", output);
    }

    [TestMethod]
    public async Task Test_PauseAsync_ContinueAlias_ResumesExecutionAsync()
    {
        (DebugResumeAction action, _) = await RunReplAsync("c\n");
        Assert.AreEqual(DebugResumeAction.Continue, action);
    }

    [TestMethod]
    public async Task Test_PauseAsync_InvalidModule_DisplaysValidationErrorsInBreakpointBannerAsync()
    {
        ValidationError error = new(nameof(ProbeModule.Name), "required", "Name is required.");

        (DebugResumeAction action, string output) = await RunReplAsync("continue\n", validationErrors: [error]);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint hit: cyborg.tests.probe.v1 name=probe [validation failed: 1 error]", output);
        Assert.Contains("Validation errors:", output);
        Assert.Contains("Name [required]: Name is required.", output);
    }

    [TestMethod]
    public async Task Test_PauseAsync_Inspect_InvalidModule_PrintsDescriptionAndValidationErrorsAsync()
    {
        ValidationError error = new(nameof(ProbeModule.Group), "test-rule", "Group is invalid.");

        (DebugResumeAction action, string output) = await RunReplAsync("inspect\ncontinue\n", validationErrors: [error]);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("cyborg.tests.probe.v1", output);
        Assert.Contains("Group [test-rule]: Group is invalid.", output);
    }

    [TestMethod]
    public async Task Test_PauseAsync_Help_UsesGeneratedCommandHelpAsync()
    {
        (DebugResumeAction action, string output) = await RunReplAsync("help\ncontinue\n");

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Commands:", output);
        Assert.Contains("break", output);
        Assert.DoesNotContain("run", output);
    }

    [TestMethod]
    public async Task Test_PauseAsync_RepeatedPauses_ReuseDispatcherAsync()
    {
        (DebugResumeAction action, _, _) = await RunReplWithRegistryAsync("continue\ncontinue\n", new BreakpointRegistry(), pauseCount: 2);

        Assert.AreEqual(DebugResumeAction.Continue, action);
    }

    [TestMethod]
    public async Task Test_PauseAsync_Inspect_PrintsStateAndStaysUntilContinueAsync()
    {
        (DebugResumeAction action, string output) = await RunReplAsync("inspect\ncontinue\n");
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("cyborg.tests.probe.v1", output);
    }

    [TestMethod]
    public async Task Test_PauseAsync_CafNestedAliases_RouteCommandsAsync()
    {
        (DebugResumeAction action, string output, IBreakpointRegistry breakpoints) = await RunReplWithRegistryAsync("b at other\nb ls\nb rm 1\ni\ns\n", new BreakpointRegistry());

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint 1 set: other", output);
        Assert.Contains("Removed breakpoint 1.", output);
        Assert.Contains("cyborg.tests.probe.v1", output);
        Assert.Contains(static breakpoint => breakpoint.Expression == ".*" && breakpoint.IsOneShot, breakpoints.ToList());
    }

    [TestMethod]
    public async Task Test_PauseAsync_UnknownCommand_StaysInReplAsync()
    {
        (DebugResumeAction action, _) = await RunReplAsync("not-a-command\nresume\n");

        Assert.AreEqual(DebugResumeAction.Continue, action);
    }

    [TestMethod]
    public async Task Test_PauseAsync_BreakLsAndRm_ManageBreakpointsAsync()
    {
        (DebugResumeAction action, string output) = await RunReplAsync("break at other\nbreak ls\nbreak rm 2\nbreak ls\ncontinue\n", seedBreakpoints: ["seed"]);
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint 2 set: other", output);
        Assert.Contains("Removed breakpoint 2.", output);
    }

    [TestMethod]
    public async Task Test_PauseAsync_BreakRm_InvalidId_StaysInReplAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("seed");

        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) = await RunReplWithRegistryAsync("break rm nope\ncontinue\n", registry);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(1, breakpoints.Count);
    }

    [TestMethod]
    public async Task Test_PauseAsync_BreakAt_QuotedExpressionPreservesSpacesAsync()
    {
        (DebugResumeAction action, string output, IBreakpointRegistry breakpoints) = await RunReplWithRegistryAsync("break at \"other module\"\ncontinue\n", new BreakpointRegistry());

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint 1 set: other module", output);
        Assert.Contains(static breakpoint => breakpoint.Expression == "other module", breakpoints.ToList());
    }

    [TestMethod]
    public async Task Test_PauseAsync_BreakAt_UnquotedExpressionJoinsRemainingTokensAsync()
    {
        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) = await RunReplWithRegistryAsync("break at other module\ncontinue\n", new BreakpointRegistry());

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(static breakpoint => breakpoint.Expression == "other module", breakpoints.ToList());
    }

    [TestMethod]
    public async Task Test_PauseAsync_Step_AddsOneShotWildcardAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("probe");
        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) = await RunReplWithRegistryAsync("step\n", registry);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(static breakpoint => breakpoint.Expression == ".*" && breakpoint.IsOneShot, breakpoints.ToList());
    }

    [TestMethod]
    public async Task Test_PauseAsync_Detach_ClearsBreakpointsAsync()
    {
        BreakpointRegistry registry = new();
        registry.Add("probe");
        registry.Add("other");
        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) = await RunReplWithRegistryAsync("detach\n", registry);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(0, breakpoints.Count);
    }

    [TestMethod]
    public async Task Test_PauseAsync_Cancel_ReturnsCancelAsync()
    {
        (DebugResumeAction action, _) = await RunReplAsync("cancel\n");
        Assert.AreEqual(DebugResumeAction.Cancel, action);
    }

    [TestMethod]
    public async Task Test_PauseAsync_Eof_DetachesAndContinuesAsync()
    {
        (DebugResumeAction action, _, IBreakpointRegistry breakpoints) = await RunReplWithRegistryAsync(string.Empty, new BreakpointRegistry());
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(0, breakpoints.Count);
    }

    [TestMethod]
    public async Task Test_PauseAsync_InvalidModule_UsesSemanticErrorOutputKindAsync()
    {
        BreakpointRegistry breakpoints = new();
        RecordingDebugReplIo io = new(["continue"]);
        using DefaultServiceProvider services = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        RootModuleRuntime runtime = new(new GlobalRuntimeEnvironment(JsonNamingPolicy.SnakeCaseLower), loggerFactory);
        ProbeModule module = new() { Name = "probe" };
        ValidationError error = new(nameof(ProbeModule.Name), "required", "Name is required.");
        IValidationResult<ProbeModule> result = ValidationResult.Invalid(module, [error]);
        DebugPauseContextStub context = new(ProbeModule.ModuleId, result, runtime, services, breakpoints, RequestStepAction: () => breakpoints.Add(".*", isOneShot: true), DetachAction: breakpoints.Clear);

        DebugResumeAction action = await CreateFrontend(io).PauseAsync(context, TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(static write => write.Message.Contains("validation failed", StringComparison.Ordinal) && write.Kind == OutputKind.Error, io.Writes);
        Assert.Contains(static write => write.Message.Contains("Name [required]: Name is required.", StringComparison.Ordinal) && write.Kind == OutputKind.Error, io.Writes);
    }

    [TestMethod]
    public async Task Test_PauseAsync_UsesPromptAwareIoAndSemanticOutputKindsAsync()
    {
        BreakpointRegistry breakpoints = new();
        RecordingDebugReplIo io = new(["break at other", "continue"]);
        using DefaultServiceProvider services = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        GlobalRuntimeEnvironment environment = new(JsonNamingPolicy.SnakeCaseLower);
        RootModuleRuntime runtime = new(environment, loggerFactory);
        ProbeModule module = new() { Name = "probe" };
        DebugPauseContextStub context = new(
            ProbeModule.ModuleId,
            ValidationResult.Valid(module),
            runtime,
            services,
            breakpoints,
            RequestStepAction: () => breakpoints.Add(".*", isOneShot: true),
            DetachAction: breakpoints.Clear);

        DebugResumeAction action = await CreateFrontend(io).PauseAsync(context, TestContext.CancellationToken);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(static prompt => prompt == "(cyborg-dbg) ", io.Prompts);
        Assert.Contains(static write => write.Message == "Breakpoint 1 set: other" && write.Kind == OutputKind.Success, io.Writes);
        Assert.Contains(static write => write.Message.StartsWith("Breakpoint hit:", StringComparison.Ordinal) && write.Kind == OutputKind.Status, io.Writes);
    }

    private async Task<(DebugResumeAction Action, string Output)> RunReplAsync(string script, string[]? seedBreakpoints = null, IReadOnlyList<ValidationError>? validationErrors = null)
    {
        BreakpointRegistry registry = new();
        if (seedBreakpoints is not null)
        {
            foreach (string expression in seedBreakpoints)
            {
                registry.Add(expression);
            }
        }

        (DebugResumeAction action, string output, _) = await RunReplWithRegistryAsync(script, registry, validationErrors: validationErrors);
        return (action, output);
    }

    private async Task<DebugResumeContext> RunReplWithRegistryAsync(string script, BreakpointRegistry registry, int pauseCount = 1, IReadOnlyList<ValidationError>? validationErrors = null)
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

        DebugPauseContextStub context = new(
            ProbeModule.ModuleId,
            new ValidationResult<ProbeModule>(module, validationErrors ?? []),
            runtime,
            services,
            registry,
            RequestStepAction: () => registry.Add(".*", isOneShot: true),
            DetachAction: registry.Clear);

        DebugResumeAction action = default;
        for (int index = 0; index < pauseCount; index++)
        {
            action = await frontend.PauseAsync(context, TestContext.CancellationToken);
        }

        return (action, outputBuilder.ToString(), registry);
    }

    private static ConsoleDebugFrontend CreateFrontend(IDebugReplIo io) => new(io, new DebugCommandDispatcher(io, new CafDebugCommandRouter()));

    private sealed record DebugPauseContextStub
    (
        string ModuleId,
        IValidationResult<IModule> ValidationResult,
        IModuleRuntime Runtime,
        IServiceProvider Services,
        IBreakpointRegistry Breakpoints,
        Action RequestStepAction,
        Action DetachAction
    ) : IDebugPauseContext
    {
        public void RequestStep() => RequestStepAction();

        public void Detach() => DetachAction();
    }

    private sealed class RecordingDebugReplIo(IEnumerable<string?> input) : IDebugReplIo
    {
        private readonly Queue<string?> _input = new(input);

        public List<string> Prompts { get; } = [];

        public List<(string Message, OutputKind Kind)> Writes { get; } = [];

        public ValueTask<string?> ReadLineAsync(string prompt, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Prompts.Add(prompt);
            return new(_input.Count == 0 ? null : _input.Dequeue());
        }

        public ValueTask WriteAsync(string message, OutputKind kind, CancellationToken cancellationToken = default)
        {
            Writes.Add((message, kind));
            return ValueTask.CompletedTask;
        }

        public ValueTask WriteLineAsync(string message, OutputKind kind, CancellationToken cancellationToken = default)
        {
            Writes.Add((message, kind));
            return ValueTask.CompletedTask;
        }
    }
}
