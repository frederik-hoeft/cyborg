using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed record ExecutionTreeNode(
    ModuleExecutionId ExecutionId,
    ModuleExecutionId? ParentExecutionId,
    string ModuleId,
    string? Name,
    string? Group,
    ExecutionTreeNodeState State,
    ModuleExitStatus? ExitStatus,
    IReadOnlyList<IExecutionTreeNode> Children) : IExecutionTreeNode;
