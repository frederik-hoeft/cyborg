namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugBranchControlState(long sessionGeneration, bool isStepping)
{
    public long SessionGeneration { get; set; } = sessionGeneration;

    public bool IsStepping { get; set; } = isStepping;
}
