using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging;

/// <summary>
/// Dispatch-local result sink injected into CAF command instances. The dispatcher owns the instance and reads the result after CAF returns.
/// </summary>
internal sealed class DebugCommandResult
{
    internal DebugResumeAction? ResumeAction { get; private set; }

    internal void Resume(DebugResumeAction action)
    {
        if (ResumeAction is not null)
        {
            throw new InvalidOperationException("A debugger command has already selected a resume action.");
        }

        ResumeAction = action;
    }
}
