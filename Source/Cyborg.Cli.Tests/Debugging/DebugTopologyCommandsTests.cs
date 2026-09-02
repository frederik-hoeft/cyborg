using Cyborg.Cli.Debugging;
using Cyborg.Cli.Tests.Mocks;
using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Runtime.Services.Validation;
using Cyborg.Core.Services.Default;
using Cyborg.Core.TestAdapter;
using Cyborg.TestModules.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Cli.Tests.Debugging;

[TestClass]
public sealed class DebugTopologyCommandsTests : CyborgCliTestBase
{
    protected override void ConfigureServices(IServiceCollection services, IJabServiceDiscovery jabServiceDiscovery)
    {
        base.ConfigureServices(services, jabServiceDiscovery);

        services.AddSingleton<TestDebugReplIoInputWriter>();
        services.AddSingleton<IDebugReplIo>(static provider => new TestDebugReplIo(provider.GetRequiredService<TestDebugReplIoInputWriter>().Input));
    }

    protected override void BuildConfiguration(IConfigurationBuilder configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        base.BuildConfiguration(configuration);

        IServiceSelectionKey<IDebugFrontend> debugFrontendKey = configuration.ServiceProvider.GetRequiredService<IServiceSelectionKey<IDebugFrontend>>();
        configuration.AddDictionary(dictionary => dictionary.AddEntry(debugFrontendKey.Key, "console"));
    }

    [TestMethod]
    public Task Test_PauseAsync_TreeAndStack_AreRegisteredAndRenderLiveSnapshotAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IDefault<IDebugFrontend> defaultFrontend = services.GetRequiredService<IDefault<IDebugFrontend>>();
        IDebugReplIo debugReplIo = services.GetRequiredService<IDebugReplIo>();
        TestDebugReplIoInputWriter inputWriter = services.GetRequiredService<TestDebugReplIoInputWriter>();
        ModuleExecutionId rootId = new(Guid.NewGuid());
        ModuleExecutionId currentId = new(Guid.NewGuid());
        ModuleExecutionId pausedId = new(Guid.NewGuid());
        ModuleExecutionId completedId = new(Guid.NewGuid());

        TestNode current = new(currentId, rootId, "cyborg.tests.current.v1", "current", null, ExecutionTreeNodeState.Current, ExitStatus: null, Children: []);
        TestNode paused = new(pausedId, rootId, "cyborg.tests.queued.v1", "queued", null, ExecutionTreeNodeState.Paused, ExitStatus: null, Children: []);
        TestNode completed = new(completedId, rootId, "cyborg.tests.completed.v1", "completed", null, ExecutionTreeNodeState.Completed, ModuleExitStatus.Success, Children: []);
        TestNode root = new(
            rootId,
            ParentExecutionId: null,
            ModuleId: "cyborg.tests.root.v1",
            Name: "root",
            Group: null,
            State: ExecutionTreeNodeState.Running,
            ExitStatus: null,
            Children: [current, paused, completed]);
        TestSnapshot tree = new([root]);
        IReadOnlyList<IExecutionTreeNode> stack = [current, root];
        ProbeModule module = new() { Name = "current" };
        DebugPauseContextStub context = new(
            ProbeModule.ModuleId,
            currentId,
            ValidationResult.Valid(module),
            runtime,
            services,
            breakpoints,
            Diagnostics: [],
            tree,
            stack);
        inputWriter.Write("help\ntree\nstack\ncontinue\n");

        DebugResumeAction action = await defaultFrontend.GetRequiredDefault().PauseAsync(context, TestContext.CancellationToken);
        Assert.IsInstanceOfType<TestDebugReplIo>(debugReplIo);
        string output = ((TestDebugReplIo)debugReplIo).Output.ToString();

        Assert.AreEqual(DebugResumeAction.Continue, action);
        Assert.Contains("tree", output);
        Assert.Contains("stack", output);
        Assert.Contains("* cyborg.tests.root.v1 name=root [running]", output);
        Assert.Contains("  * cyborg.tests.current.v1 name=current [paused/current]", output);
        Assert.Contains("  * cyborg.tests.queued.v1 name=queued [paused]", output);
        Assert.Contains("  * cyborg.tests.completed.v1 name=completed [completed: Success]", output);
        Assert.Contains("#0 cyborg.tests.current.v1 name=current [paused/current]", output);
        Assert.Contains("#1 cyborg.tests.root.v1 name=root [running]", output);
    });

    [TestMethod]
    public void Test_Formatter_EmptyViews_RenderExplicitPlaceholders()
    {
        Assert.AreEqual("(no active execution)", ExecutionTreeFormatter.FormatTree(new TestSnapshot([])));
        Assert.AreEqual("(no active stack)", ExecutionTreeFormatter.FormatStack([]));
    }

    private sealed record DebugPauseContextStub(
        string ModuleId,
        ModuleExecutionId? ExecutionId,
        IValidationResult<IModule> ValidationResult,
        IModuleRuntime Runtime,
        IServiceProvider Services,
        IBreakpointRegistry Breakpoints,
        IReadOnlyList<DebugDiagnostic> Diagnostics,
        IExecutionTreeSnapshot Tree,
        IReadOnlyList<IExecutionTreeNode> Stack) : IDebugPauseContext;

    private sealed record TestSnapshot(IReadOnlyList<IExecutionTreeNode> Roots) : IExecutionTreeSnapshot;

    private sealed record TestNode(
        ModuleExecutionId ExecutionId,
        ModuleExecutionId? ParentExecutionId,
        string ModuleId,
        string? Name,
        string? Group,
        ExecutionTreeNodeState State,
        ModuleExitStatus? ExitStatus,
        IReadOnlyList<IExecutionTreeNode> Children) : IExecutionTreeNode;
}
