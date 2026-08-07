using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging;

/// <summary>
/// Console-based debug frontend. The frontend owns REPL lifecycle and I/O while command dispatch and grammar are separate services.
/// </summary>
internal sealed class ConsoleDebugFrontend(IDebugReplIo io, DebugCommandDispatcher commandDispatcher) : IDebugFrontend
{
    private const string PROMPT = "(cyborg-dbg) ";
    private readonly IDebugReplIo _io = io ?? throw new ArgumentNullException(nameof(io));
    private readonly DebugCommandDispatcher _commandDispatcher = commandDispatcher ?? throw new ArgumentNullException(nameof(commandDispatcher));

    public string Key => "console";

    public async ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        _io.WriteLine(string.Empty);
        _io.WriteLine($"Breakpoint hit: {context.ModuleIdentity}", DebugReplOutputKind.Status);
        _io.WriteLine("Type 'help' for available commands.", DebugReplOutputKind.Status);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await _io.ReadLineAsync(PROMPT, cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                // EOF: detach and continue so unattended pipes do not hang forever.
                context.Detach();
                return DebugResumeAction.Continue;
            }

            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            DebugResumeAction? action = await _commandDispatcher.DispatchAsync(line, context, cancellationToken).ConfigureAwait(false);
            if (action is not null)
            {
                return action.Value;
            }
        }
    }
}
