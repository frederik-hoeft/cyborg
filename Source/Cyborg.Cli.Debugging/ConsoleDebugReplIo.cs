namespace Cyborg.Cli.Debugging;

internal sealed class ConsoleDebugReplIo : IDebugReplIo
{
    public async ValueTask<string?> ReadLineAsync(string prompt, CancellationToken cancellationToken)
    {
        await Console.Out.WriteAsync(prompt);
        return await Console.In.ReadLineAsync(cancellationToken);
    }

    public ValueTask WriteAsync(string message, OutputKind kind, CancellationToken cancellationToken = default) =>
        new(Console.Out.WriteAsync(message.AsMemory(), cancellationToken));

    public ValueTask WriteLineAsync(string message, OutputKind kind, CancellationToken cancellationToken = default) =>
        new(Console.Out.WriteLineAsync(message.AsMemory(), cancellationToken));
}
