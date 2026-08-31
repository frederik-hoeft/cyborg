using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Services.Debugging;

/// <summary>Read-only access to the debugger's current logical execution topology.</summary>
public interface IDebugExecutionTopology
{
    /// <summary>Captures all currently open logical module executions.</summary>
    IExecutionTreeSnapshot CaptureTree();

    /// <summary>Captures the specified open execution followed by its logical ancestors up to the root.</summary>
    IReadOnlyList<IExecutionTreeNode> CaptureAncestry(ModuleExecutionId executionId);
}
