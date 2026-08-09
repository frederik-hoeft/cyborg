namespace Cyborg.Cli.Debugging;

/// <summary>
/// I/O abstraction for interactive debugger REPLs. Implementations may provide richer prompt and output rendering without affecting command execution.
/// </summary>
public interface IDebugReplIo
{
    ValueTask WriteAsync(string message, OutputKind kind, CancellationToken cancellationToken = default);

    ValueTask WriteLineAsync(string message, OutputKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders <paramref name="prompt"/> and reads the next line of user input, or returns null on EOF.
    /// Keeping prompt rendering within this operation allows richer implementations to own interactive prompt behavior.
    /// </summary>
    ValueTask<string?> ReadLineAsync(string prompt, CancellationToken cancellationToken);
}
