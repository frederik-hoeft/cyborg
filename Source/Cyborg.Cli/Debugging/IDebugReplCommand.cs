using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging;

/// <summary>
/// Extensible REPL command handled by the console debug frontend.
/// New commands can be registered via DI without modifying the frontend loop.
/// </summary>
public interface IDebugReplCommand
{
    /// <summary>
    /// Primary command name as typed by the user (case-insensitive), e.g. "continue", "inspect", "break".
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Optional alternate names (e.g. "c" for continue, "s" for step).
    /// </summary>
    IReadOnlyList<string> Aliases { get; }

    /// <summary>
    /// Short help text shown by the help command.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Attempts to execute the command. Returns false if the command cannot handle the input
    /// (e.g. wrong arity); the frontend will report an error.
    /// </summary>
    ValueTask<DebugReplCommandResult> ExecuteAsync(
        DebugReplCommandInput input,
        IDebugPauseContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Parsed user input for a single REPL line.
/// </summary>
public sealed class DebugReplCommandInput
{
    public DebugReplCommandInput(string rawLine, string commandName, string arguments)
    {
        RawLine = rawLine;
        CommandName = commandName;
        Arguments = arguments;
    }

    public string RawLine { get; }

    public string CommandName { get; }

    /// <summary>
    /// Remainder of the line after the command name (trimmed). May be empty.
    /// </summary>
    public string Arguments { get; }
}

/// <summary>
/// Result of executing a REPL command.
/// </summary>
public sealed class DebugReplCommandResult
{
    private DebugReplCommandResult(bool stayInRepl, DebugResumeAction? resumeAction, string? errorMessage)
    {
        StayInRepl = stayInRepl;
        ResumeAction = resumeAction;
        ErrorMessage = errorMessage;
    }

    public bool StayInRepl { get; }

    public DebugResumeAction? ResumeAction { get; }

    public string? ErrorMessage { get; }

    public static DebugReplCommandResult ContinueInRepl() => new(stayInRepl: true, resumeAction: null, errorMessage: null);

    public static DebugReplCommandResult Resume(DebugResumeAction action) => new(stayInRepl: false, resumeAction: action, errorMessage: null);

    public static DebugReplCommandResult Error(string message) => new(stayInRepl: true, resumeAction: null, errorMessage: message);
}
