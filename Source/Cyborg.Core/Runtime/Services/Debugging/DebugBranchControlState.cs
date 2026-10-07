using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugBranchControlState(
    long sessionGeneration,
    bool isStepping,
    ModuleExecutionId? stepOverAnchor = null,
    long controlCommandSequence = 0,
    bool requiresCommandOrdering = false)
{
    public long SessionGeneration { get; set; } = sessionGeneration;

    /// <summary>Sequence of the latest explicit debugger control command represented by this state.</summary>
    public long ControlCommandSequence { get; set; } = controlCommandSequence;

    /// <summary>Whether reconciliation must preserve latest-command ordering because step-over participated in this debugger session.</summary>
    public bool RequiresCommandOrdering { get; set; } = requiresCommandOrdering;

    public bool IsStepping { get; set; } = isStepping;

    /// <summary>Invocation being stepped over. The built-in control keeps this unset while <see cref="IsStepping"/> is set.</summary>
    public ModuleExecutionId? StepOverAnchor { get; set; } = stepOverAnchor;
}
