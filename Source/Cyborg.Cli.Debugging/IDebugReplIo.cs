namespace Cyborg.Cli.Debugging;

/// <summary>
/// Abstraction over console I/O for the debug REPL, enabling tests to feed scripted input
/// without blocking on a real terminal.
/// </summary>
internal interface IDebugReplIo
{
    void WriteLine(string message);

    void Write(string message);

    /// <summary>
    /// Reads the next line of user input, or null on EOF.
    /// </summary>
    ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken);
}
