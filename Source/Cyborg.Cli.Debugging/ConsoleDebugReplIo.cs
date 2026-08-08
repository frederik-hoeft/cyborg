namespace Cyborg.Cli.Debugging;

internal sealed class ConsoleDebugReplIo : IDebugReplIo
{
    public async ValueTask<string?> ReadLineAsync(string prompt, CancellationToken cancellationToken)
    {
        await Console.Out.WriteAsync(prompt);
        return await Console.In.ReadLineAsync(cancellationToken);
    }

    public async ValueTask WriteAsync(string message, OutputKind kind, CancellationToken cancellationToken = default) => await Console.Out.WriteAsync(message);

    public async ValueTask WriteLineAsync(string message, OutputKind kind, CancellationToken cancellationToken = default) => await Console.Out.WriteLineAsync(message);
}
