namespace Cyborg.Cli.Debugging;

/// <summary>
/// Scripted I/O for tests and unattended automation.
/// </summary>
internal sealed class TextDebugReplIo(TextReader input, TextWriter output) : IDebugReplIo
{
    public async ValueTask<string?> ReadLineAsync(string prompt, CancellationToken cancellationToken)
    {
        await output.WriteAsync(prompt);
        return await input.ReadLineAsync(cancellationToken);
    }

    public async ValueTask WriteAsync(string message, OutputKind kind, CancellationToken cancellationToken = default) => await output.WriteAsync(message);

    public async ValueTask WriteLineAsync(string message, OutputKind kind, CancellationToken cancellationToken = default) => await output.WriteLineAsync(message);
}
