namespace Cyborg.Cli.Debugging;

/// <summary>
/// Scripted I/O for tests and unattended automation.
/// </summary>
internal sealed class TextDebugReplIo(TextReader input, TextWriter output) : IDebugReplIo
{
    public void WriteLine(string message) => output.WriteLine(message);

    public void Write(string message) => output.Write(message);

    public ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) => input.ReadLineAsync(cancellationToken);
}
