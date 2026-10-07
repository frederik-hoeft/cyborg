using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugBranchControlState(long sessionGeneration, bool isStepping, ModuleExecutionId? stepOverAnchor = null)
{
    public long SessionGeneration { get; set; } = sessionGeneration;

    public bool IsStepping { get; set; } = isStepping;

    /// <summary>Invocation being stepped over. The built-in control keeps this unset while <see cref="IsStepping"/> is set.</summary>
    public ModuleExecutionId? StepOverAnchor { get; set; } = stepOverAnchor;
}
