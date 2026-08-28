using Cyborg.Core.Services.Default;

namespace Cyborg.Core.Runtime.Services.Debugging;

/// <summary>
/// Adapter that presents a debug pause to the user (console REPL, remote debugger, web UI, etc.).
/// Implementations own all I/O; the runtime only supplies <see cref="IDebugPauseContext"/>.
/// </summary>
public interface IDebugFrontend : IKeyedService
{
    /// <summary>
    /// Runs an interactive session at a breakpoint and returns when execution should resume or cancel.
    /// </summary>
    ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken);
}
