using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging;

/// <summary>
/// Stateless bridge from one REPL input line to the debugger command router. Command state is dispatch-local and supplied to CAF through DI.
/// </summary>
internal sealed class DebugCommandDispatcher(IDebugReplIo io, CafDebugCommandRouter router)
{
    private readonly IDebugReplIo _io = io ?? throw new ArgumentNullException(nameof(io));
    private readonly CafDebugCommandRouter _router = router ?? throw new ArgumentNullException(nameof(router));

    internal async ValueTask<DebugResumeAction?> DispatchAsync(string commandLine, IDebugPauseContext context, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!CommandLineTokenizer.TryTokenize(commandLine, out string[] arguments, out string? error))
        {
            _io.WriteLine(error!, DebugReplOutputKind.Error);
            return null;
        }
        if (arguments.Length == 0)
        {
            return null;
        }

        DebugCommandResult result = new();
        DebugCommandServiceProvider services = new(context, result, _io);
        await _router.RunAsync(arguments, services, _io, cancellationToken).ConfigureAwait(false);
        return result.ResumeAction;
    }
}
