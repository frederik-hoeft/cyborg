using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed record DebugPauseContext
(
    string ModuleId,
    ModuleExecutionId? ExecutionId,
    IValidationResult<IModule> ValidationResult,
    IModuleRuntime Runtime,
    IServiceProvider Services,
    IBreakpointRegistry Breakpoints,
    IReadOnlyList<DebugDiagnostic> Diagnostics,
    IDebugExecutionTopology Topology
) : IDebugPauseContext
{
    public IExecutionTreeSnapshot Tree => Topology.CaptureTree();

    public IReadOnlyList<IExecutionTreeNode> Stack => ExecutionId is { } executionId ? Topology.CaptureAncestry(executionId) : [];
}
