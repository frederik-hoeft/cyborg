namespace Cyborg.Core.Runtime.Services.Debugging;

/// <summary>Point-in-time state of one open logical module execution.</summary>
public enum ExecutionTreeNodeState
{
    /// <summary>The invocation is active and has not yet produced a definite result.</summary>
    Running = 0,

    /// <summary>The invocation has produced a result but its structured execution scope has not closed yet.</summary>
    Completed = 1,

    /// <summary>The invocation has decided to pause and is waiting for frontend ownership.</summary>
    Paused = 2,

    /// <summary>The invocation is the pause point whose frontend session is currently active.</summary>
    Current = 3,
}
