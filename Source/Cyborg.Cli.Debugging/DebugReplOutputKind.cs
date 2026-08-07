namespace Cyborg.Cli.Debugging;

/// <summary>
/// Semantic output categories emitted by the debugger REPL. Frontends may render categories differently while plain-text implementations may ignore them.
/// </summary>
public enum DebugReplOutputKind
{
    Text = 0,
    Status = 1,
    Success = 2,
    Warning = 3,
    Error = 4,
}
