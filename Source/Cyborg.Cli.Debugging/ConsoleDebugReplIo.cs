namespace Cyborg.Cli.Debugging;

internal sealed class ConsoleDebugReplIo : IDebugReplIo
{
    public void WriteLine(string message) => Console.Out.WriteLine(message);

    public void Write(string message) => Console.Out.Write(message);

    public ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) => Console.In.ReadLineAsync(cancellationToken);
}
