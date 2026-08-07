namespace Cyborg.Cli.Debugging;

internal sealed class ConsoleDebugReplIo : IDebugReplIo
{
    public void WriteLine(string message, DebugReplOutputKind kind = DebugReplOutputKind.Text) => Console.Out.WriteLine(message);

    public void Write(string message, DebugReplOutputKind kind = DebugReplOutputKind.Text) => Console.Out.Write(message);

    public ValueTask<string?> ReadLineAsync(string prompt, CancellationToken cancellationToken)
    {
        Console.Out.Write(prompt);
        return Console.In.ReadLineAsync(cancellationToken);
    }
}
