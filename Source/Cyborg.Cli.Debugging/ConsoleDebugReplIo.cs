namespace Cyborg.Cli.Debugging;

internal sealed class ConsoleDebugReplIo : IDebugReplIo
{
    public void WriteLine(string message, DebugReplOutputKind kind = DebugReplOutputKind.Text) => Console.Out.WriteLine(message);

    public void Write(string message, DebugReplOutputKind kind = DebugReplOutputKind.Text) => Console.Out.Write(message);

    public async ValueTask<string?> ReadLineAsync(string prompt, CancellationToken cancellationToken)
    {
        await Console.Out.WriteAsync(prompt);
        return await Console.In.ReadLineAsync(cancellationToken);
    }
}
