using Cyborg.Cli.Debugging;
using Cyborg.Cli.Tests.Mocks;
using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Services.Default;
using Cyborg.Core.TestAdapter;
using Cyborg.TestModules.Cli;
using Microsoft.Extensions.DependencyInjection;
using DebugReplResult = (Cyborg.Core.Modules.Debugging.DebugResumeAction Action, string Output);

namespace Cyborg.Cli.Tests.Debugging;

[TestClass]
public sealed class ConsoleDebugFrontendTests : CyborgCliTestBase
{
    protected override void ConfigureServices(IServiceCollection services, IJabServiceDiscovery jabServiceDiscovery)
    {
        base.ConfigureServices(services, jabServiceDiscovery);

        services.AddSingleton<TestDebugReplIoInputWriter>();
        services.AddSingleton<IDebugReplIo>(static sp => new TestDebugReplIo(sp.GetRequiredService<TestDebugReplIoInputWriter>().Input));
    }

    protected override void BuildConfiguration(IConfigurationBuilder configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        base.BuildConfiguration(configuration);

        IServiceSelectionKey<IDebugFrontend> debugFrontendKey = configuration.ServiceProvider.GetRequiredService<IServiceSelectionKey<IDebugFrontend>>();
        configuration.AddDictionary(dict => dict.AddEntry(debugFrontendKey.Key, "console"));
    }

    [TestMethod]
    public Task Test_DefaultFrontend_ResolvesConsoleFrontendAsync() => TestWithDIAsync(services =>
    {
        IDefault<IDebugFrontend> defaultFrontend = services.GetRequiredService<IDefault<IDebugFrontend>>();

        Assert.AreEqual("console", defaultFrontend.GetRequiredDefault().Key);
    });

    [TestMethod]
    public Task Test_PauseAsync_Continue_ResumesExecutionAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, string output) = await RunReplAsync(services, "continue\n");
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint hit:", output);
    });

    [TestMethod]
    public Task Test_PauseAsync_ContinueAlias_ResumesExecutionAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, string output) = await RunReplAsync(services, "c\n");
        Assert.AreEqual(DebugResumeAction.Continue, action);
    });

    [TestMethod]
    public Task Test_PauseAsync_InvalidModule_DisplaysValidationErrorsInBreakpointBannerAsync() => TestWithDIAsync(async services =>
    {
        ValidationError error = new(nameof(ProbeModule.Name), "required", "Name is required.");

        (DebugResumeAction action, string output) = await RunReplAsync(services, "continue\n", validationErrors: [error]);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint hit: cyborg.tests.probe.v1 name=probe [validation failed: 1 error]", output);
        Assert.Contains("Validation errors:", output);
        Assert.Contains("Name [required]: Name is required.", output);
    });

    [TestMethod]
    public Task Test_PauseAsync_Inspect_InvalidModule_PrintsDescriptionAndValidationErrorsAsync() => TestWithDIAsync(async services =>
    {
        ValidationError error = new(nameof(ProbeModule.Group), "test-rule", "Group is invalid.");

        (DebugResumeAction action, string output) = await RunReplAsync(services, "inspect\ncontinue\n", validationErrors: [error]);

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("cyborg.tests.probe.v1", output);
        Assert.Contains("Group [test-rule]: Group is invalid.", output);
    });

    [TestMethod]
    public Task Test_PauseAsync_Help_UsesGeneratedCommandHelpAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, string output) = await RunReplAsync(services, "help\ncontinue\n");

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Commands:", output);
        Assert.Contains("break", output);
        Assert.DoesNotContain("run", output);
    });

    [TestMethod]
    public Task Test_PauseAsync_RepeatedPauses_ReuseDispatcherAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, _) = await RunReplAsync(services, "continue\ncontinue\n", pauseCount: 2);

        Assert.AreEqual(DebugResumeAction.Continue, action);
    });

    [TestMethod]
    public Task Test_PauseAsync_Inspect_PrintsStateAndStaysUntilContinueAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, string output) = await RunReplAsync(services, "inspect\ncontinue\n");
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("cyborg.tests.probe.v1", output);
    });

    [TestMethod]
    public Task Test_PauseAsync_CafNestedAliases_RouteCommandsAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, string output) = await RunReplAsync(services, "b at other\nb ls\nb rm 1\ni\ns\n");
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint 1 set: other", output);
        Assert.Contains("Removed breakpoint 1.", output);
        Assert.Contains("cyborg.tests.probe.v1", output);
        Assert.Contains(static breakpoint => breakpoint.Expression == ".*" && breakpoint.IsOneShot, breakpoints.ToList());
    });

    [TestMethod]
    public Task Test_PauseAsync_UnknownCommand_StaysInReplAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, _) = await RunReplAsync(services, "not-a-command\nresume\n");

        Assert.AreEqual(DebugResumeAction.Continue, action);
    });

    [TestMethod]
    public Task Test_PauseAsync_BreakLsAndRm_ManageBreakpointsAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, string output) = await RunReplAsync(services, "break at other\nbreak ls\nbreak rm 2\nbreak ls\ncontinue\n", seedBreakpoints: ["seed"]);
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint 2 set: other", output);
        Assert.Contains("Removed breakpoint 2.", output);
    });

    [TestMethod]
    public Task Test_PauseAsync_BreakRm_InvalidId_StaysInReplAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, _) = await RunReplAsync(services, "break rm nope\ncontinue\n", ["seed"]);
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(1, breakpoints.Count);
    });

    [TestMethod]
    public Task Test_PauseAsync_BreakAt_QuotedExpressionPreservesSpacesAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, string output) = await RunReplAsync(services, "break at \"other module\"\ncontinue\n");
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("Breakpoint 1 set: other module", output);
        Assert.Contains(static breakpoint => breakpoint.Expression == "other module", breakpoints.ToList());
    });

    [TestMethod]
    public Task Test_PauseAsync_BreakAt_UnquotedExpressionJoinsRemainingTokensAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, _) = await RunReplAsync(services, "break at other module\ncontinue\n");
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(static breakpoint => breakpoint.Expression == "other module", breakpoints.ToList());
    });

    [TestMethod]
    public Task Test_PauseAsync_Step_AddsOneShotWildcardAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, _) = await RunReplAsync(services, "step\n", ["probe"]);
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(static breakpoint => breakpoint.Expression == ".*" && breakpoint.IsOneShot, breakpoints.ToList());
    });

    [TestMethod]
    public Task Test_PauseAsync_Detach_ClearsBreakpointsAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, _) = await RunReplAsync(services, "detach\n", ["probe", "other"]);
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(0, breakpoints.Count);
    });

    [TestMethod]
    public Task Test_PauseAsync_Cancel_ReturnsCancelAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, _) = await RunReplAsync(services, "cancel\n");
        Assert.AreEqual(DebugResumeAction.Cancel, action);
    });

    [TestMethod]
    public Task Test_PauseAsync_Eof_DetachesAndContinuesAsync() => TestWithDIAsync(async services =>
    {
        (DebugResumeAction action, _) = await RunReplAsync(services, string.Empty);
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.AreEqual(0, breakpoints.Count);
    });

    [TestMethod]
    public Task Test_PauseAsync_InvalidModule_UsesSemanticErrorOutputKindAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IDebugFrontend frontend = services.GetRequiredService<IDebugFrontend>();
        IDebugReplIo io = services.GetRequiredService<IDebugReplIo>();

        ProbeModule module = new() { Name = "probe" };

        ValidationError error = new(nameof(ProbeModule.Name), "required", "Name is required.");
        IValidationResult<ProbeModule> result = ValidationResult.Invalid(module, [error]);
        DebugPauseContextStub context = new(
            ProbeModule.ModuleId,
            result,
            runtime,
            services,
            breakpoints,
            RequestStepAction: () => breakpoints.Add(".*", isOneShot: true),
            DetachAction: breakpoints.Clear);

        DebugResumeAction action = await frontend.PauseAsync(context, TestContext.CancellationToken);
        Assert.IsInstanceOfType<RecordingDebugReplIo>(io);
        RecordingDebugReplIo typedIo = (RecordingDebugReplIo)io;

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(static write => write.Message.Contains("validation failed", StringComparison.Ordinal) && write.Kind == OutputKind.Error, typedIo.Writes);
        Assert.Contains(static write => write.Message.Contains("Name [required]: Name is required.", StringComparison.Ordinal) && write.Kind == OutputKind.Error, typedIo.Writes);
    }, configureServices: static services => services.AddSingleton<IDebugReplIo>(static _ => new RecordingDebugReplIo(["continue"])));

    [TestMethod]
    public Task Test_PauseAsync_UsesPromptAwareIoAndSemanticOutputKindsAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IDebugFrontend frontend = services.GetRequiredService<IDebugFrontend>();
        IDebugReplIo io = services.GetRequiredService<IDebugReplIo>();

        ProbeModule module = new() { Name = "probe" };
        DebugPauseContextStub context = new(
            ProbeModule.ModuleId,
            ValidationResult.Valid(module),
            runtime,
            services,
            breakpoints,
            RequestStepAction: () => breakpoints.Add(".*", isOneShot: true),
            DetachAction: breakpoints.Clear);

        DebugResumeAction action = await frontend.PauseAsync(context, TestContext.CancellationToken);
        Assert.IsInstanceOfType<RecordingDebugReplIo>(io);
        RecordingDebugReplIo typedIo = (RecordingDebugReplIo)io;

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains(static prompt => prompt == "(cyborg-dbg) ", typedIo.Prompts);
        Assert.Contains(static write => write.Message == "Breakpoint 1 set: other" && write.Kind == OutputKind.Success, typedIo.Writes);
        Assert.Contains(static write => write.Message.StartsWith("Breakpoint hit:", StringComparison.Ordinal) && write.Kind == OutputKind.Status, typedIo.Writes);
    }, configureServices: static services => services.AddSingleton<IDebugReplIo>(static _ => new RecordingDebugReplIo(["break at other", "continue"])));

    private async Task<DebugReplResult> RunReplAsync(IServiceProvider services, string script, string[]? seedBreakpoints = null, int pauseCount = 1, IReadOnlyList<ValidationError>? validationErrors = null)
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IBreakpointRegistry registry = services.GetRequiredService<IBreakpointRegistry>();
        IDebugFrontend frontend = services.GetRequiredService<IDebugFrontend>();
        IDebugReplIo debugReplIo = services.GetRequiredService<IDebugReplIo>();
        TestDebugReplIoInputWriter inputWriter = services.GetRequiredService<TestDebugReplIoInputWriter>();

        if (seedBreakpoints is not null)
        {
            foreach (string expression in seedBreakpoints)
            {
                registry.Add(expression);
            }
        }

        inputWriter.Write(script);

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

        Assert.IsInstanceOfType<TestDebugReplIo>(debugReplIo);
        TestDebugReplIo testIo = (TestDebugReplIo)debugReplIo;

        return (action, testIo.Output.ToString());
    }

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
