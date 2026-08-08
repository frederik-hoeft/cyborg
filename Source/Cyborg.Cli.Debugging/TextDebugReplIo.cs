namespace Cyborg.Cli.Debugging;

/// <summary>
/// Scripted I/O for tests and unattended automation.
/// </summary>
internal sealed class TextDebugReplIo(TextReader input, TextWriter output) : IDebugReplIo
{
    public void WriteLine(string message, DebugReplOutputKind kind = DebugReplOutputKind.Text) => output.WriteLine(message);

    public void Write(string message, DebugReplOutputKind kind = DebugReplOutputKind.Text) => output.Write(message);

    public async ValueTask<string?> ReadLineAsync(string prompt, CancellationToken cancellationToken)
    {
        await output.WriteAsync(prompt);
        return await input.ReadLineAsync(cancellationToken);
    }
}
