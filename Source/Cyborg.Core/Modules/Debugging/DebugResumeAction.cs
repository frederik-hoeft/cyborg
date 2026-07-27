namespace Cyborg.Core.Modules.Debugging;

/// <summary>
/// Disposition returned when a debug pause ends and workflow execution may proceed.
/// </summary>
public enum DebugResumeAction
{
    /// <summary>
    /// Continue executing the current module and the rest of the workflow.
    /// </summary>
    Continue = 0,

    /// <summary>
    /// Cancel the current module without executing it (workflow cancellation path).
    /// </summary>
    Cancel = 1,
}
