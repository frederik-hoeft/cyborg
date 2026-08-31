namespace Cyborg.Core.Runtime.Services.Debugging;

/// <summary>
/// Provides transaction-scoped debugger control state for the current execution branch.
/// </summary>
public interface IDebugBranchControl
{
    /// <summary>
    /// Gets whether the current execution branch is in step mode for the active debugger session.
    /// </summary>
    bool IsStepping { get; }

    /// <summary>Leaves the current execution branch in step mode after the current pause resumes.</summary>
    void Step();

    /// <summary>Clears step mode for the current execution branch.</summary>
    void Continue();
}
