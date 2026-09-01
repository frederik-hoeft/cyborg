namespace Cyborg.Core.Runtime.Services.Debugging;

/// <summary>
/// Disposition returned by a debug frontend when the current pause ends.
/// </summary>
public enum DebugResumeAction
{
    /// <summary>
    /// Continue executing the current branch until another breakpoint or step boundary pauses it.
    /// </summary>
    Continue = 0,

    /// <summary>
    /// Cancel the current module without executing it (workflow cancellation path).
    /// </summary>
    Cancel = 1,

    /// <summary>
    /// Continue while leaving the current execution branch in step mode.
    /// </summary>
    Step = 2,

    /// <summary>
    /// End the current debugger session, clearing global breakpoints and invalidating branch-local step state.
    /// </summary>
    Detach = 3,
}
