using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class DebugPauseContextTests
{
    [TestMethod]
    public void Test_TreeAndStack_CaptureFreshImmutableTopologyProjections()
    {
        ModuleExecutionId executionId = new(Guid.NewGuid());
        TestModule module = new() { Name = "paused" };
        RecordingTopology topology = new(executionId);
        DebugPauseContext context = new(
            TestModule.MODULE_ID,
            executionId,
            ValidationResult.Valid(module),
            Runtime: null!,
            Services: null!,
            Breakpoints: null!,
            Diagnostics: [],
            Topology: topology);

        IExecutionTreeSnapshot firstTree = context.Tree;
        IExecutionTreeSnapshot secondTree = context.Tree;
        IReadOnlyList<IExecutionTreeNode> firstStack = context.Stack;
        IReadOnlyList<IExecutionTreeNode> secondStack = context.Stack;

        Assert.AreEqual(executionId, context.ExecutionId);
        Assert.AreEqual(2, topology.TreeCaptureCount);
        Assert.AreEqual(2, topology.AncestryCaptureCount);
        Assert.AreEqual(executionId, topology.LastAncestryExecutionId);
        Assert.AreNotSame(firstTree, secondTree);
        Assert.AreNotSame(firstStack, secondStack);
    }

    [TestMethod]
    public void Test_Stack_WithoutExecutionIdentity_IsEmptyWithoutQueryingTopology()
    {
        TestModule module = new();
        RecordingTopology topology = new(new ModuleExecutionId(Guid.NewGuid()));
        DebugPauseContext context = new(
            TestModule.MODULE_ID,
            ExecutionId: null,
            ValidationResult: ValidationResult.Valid(module),
            Runtime: null!,
            Services: null!,
            Breakpoints: null!,
            Diagnostics: [],
            Topology: topology);

        IReadOnlyList<IExecutionTreeNode> stack = context.Stack;

        Assert.HasCount(0, stack);
        Assert.AreEqual(0, topology.AncestryCaptureCount);
    }


    [TestMethod]
    public void Test_UnscopedCustomContext_UsesEmptyTopologyDefaults()
    {
        TestModule module = new();
        IDebugPauseContext context = new MinimalPauseContext(
            TestModule.MODULE_ID,
            ValidationResult.Valid(module),
            Runtime: null!,
            Services: null!,
            Breakpoints: null!,
            Diagnostics: []);

        Assert.IsNull(context.ExecutionId);
        Assert.HasCount(0, context.Tree.Roots);
        Assert.HasCount(0, context.Stack);
    }

    private sealed record TestModule : ModuleBase
    {
        public const string MODULE_ID = "cyborg.tests.debug-pause-context.v1";
    }

    private sealed class RecordingTopology(ModuleExecutionId executionId) : IDebugExecutionTopology
    {
        public int TreeCaptureCount { get; private set; }

        public int AncestryCaptureCount { get; private set; }

        public ModuleExecutionId? LastAncestryExecutionId { get; private set; }

        public IExecutionTreeSnapshot CaptureTree()
        {
            TreeCaptureCount++;
            return new TestSnapshot([CreateNode(executionId)]);
        }

        public IReadOnlyList<IExecutionTreeNode> CaptureAncestry(ModuleExecutionId requestedExecutionId)
        {
            AncestryCaptureCount++;
            LastAncestryExecutionId = requestedExecutionId;
            return [CreateNode(requestedExecutionId)];
        }

        private static IExecutionTreeNode CreateNode(ModuleExecutionId nodeExecutionId) =>
            new TestNode(
                nodeExecutionId,
                ParentExecutionId: null,
                ModuleId: TestModule.MODULE_ID,
                Name: "paused",
                Group: null,
                State: ExecutionTreeNodeState.Current,
                ExitStatus: null,
                Children: []);
    }


    private sealed record MinimalPauseContext(
        string ModuleId,
        IValidationResult<IModule> ValidationResult,
        IModuleRuntime Runtime,
        IServiceProvider Services,
        IBreakpointRegistry Breakpoints,
        IReadOnlyList<DebugDiagnostic> Diagnostics) : IDebugPauseContext;

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
