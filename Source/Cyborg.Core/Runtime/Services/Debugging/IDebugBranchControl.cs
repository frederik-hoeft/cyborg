using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Services.Debugging;

/// <summary>
/// Provides transaction-scoped debugger control state for the current execution branch.
/// </summary>
public interface IDebugBranchControl
{
    /// <summary>
    /// Gets whether the current execution branch is in step-into mode for the active debugger session.
    /// </summary>
    /// <remarks>
    /// Step-into and step-over are mutually exclusive. A pending <see cref="StepOverAnchor"/> is not step-into.
    /// </remarks>
    bool IsStepping { get; }

    /// <summary>
    /// Gets the invocation whose subtree <c>next</c> is stepping over, when that command is armed for the active debugger session.
    /// </summary>
    /// <remarks>
    /// Descendants of this invocation do not pause for step-over. The next prepared boundary on this branch that is outside the
    /// invocation does. <see langword="null"/> means step-over is not armed. A debugger-session generation change hides an anchor
    /// captured by the previous session.
    /// </remarks>
    ModuleExecutionId? StepOverAnchor { get; }

    /// <summary>Leaves the current execution branch in step-into mode and clears any step-over anchor.</summary>
    void Step();

    /// <summary>
    /// Arms step-over at <paramref name="anchor"/> and clears step-into mode.
    /// The next prepared module boundary on this branch outside <paramref name="anchor"/> pauses.
    /// </summary>
    void Next(ModuleExecutionId anchor);

    /// <summary>Clears step-into mode and any step-over anchor for the current execution branch.</summary>
    void Continue();
}
