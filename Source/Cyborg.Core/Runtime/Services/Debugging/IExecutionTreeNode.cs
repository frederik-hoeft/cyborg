using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Services.Debugging;

/// <summary>One immutable node in a point-in-time logical execution-tree snapshot.</summary>
public interface IExecutionTreeNode
{
    /// <summary>Stable identity of this logical module invocation.</summary>
    ModuleExecutionId ExecutionId { get; }

    /// <summary>Stable identity of the logical parent invocation, or <see langword="null"/> for a root.</summary>
    ModuleExecutionId? ParentExecutionId { get; }

    /// <summary>Module loader identity captured when the invocation started.</summary>
    string ModuleId { get; }

    /// <summary>Current debugger-visible module name, enriched after preparation when available.</summary>
    string? Name { get; }

    /// <summary>Current debugger-visible module group, enriched after preparation when available.</summary>
    string? Group { get; }

    /// <summary>Current state of the open invocation.</summary>
    ExecutionTreeNodeState State { get; }

    /// <summary>Set when <see cref="State"/> is <see cref="ExecutionTreeNodeState.Completed"/>.</summary>
    ModuleExitStatus? ExitStatus { get; }

    /// <summary>Open logical child invocations in structured start order.</summary>
    IReadOnlyList<IExecutionTreeNode> Children { get; }
}
