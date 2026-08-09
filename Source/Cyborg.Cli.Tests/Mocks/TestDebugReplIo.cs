using Cyborg.Cli.Debugging;
using System.Text;

namespace Cyborg.Cli.Tests.Mocks;

/// <summary>
/// Scripted I/O for tests and unattended automation.
/// </summary>
internal sealed class TestDebugReplIo(TextReader input) : IDebugReplIo
{
    public TextReader Input => input;

    public StringBuilder Output { get; } = new StringBuilder();

    private StringWriter OutputWriter => field ??= new StringWriter(Output);

    public async ValueTask<string?> ReadLineAsync(string prompt, CancellationToken cancellationToken)
    {
        await OutputWriter.WriteAsync(prompt);
        return await input.ReadLineAsync(cancellationToken);
    }

    public ValueTask WriteAsync(string message, OutputKind kind, CancellationToken cancellationToken = default) =>
        new(OutputWriter.WriteAsync(message.AsMemory(), cancellationToken));

    public ValueTask WriteLineAsync(string message, OutputKind kind, CancellationToken cancellationToken = default) =>
        new(OutputWriter.WriteLineAsync(message.AsMemory(), cancellationToken));
}
