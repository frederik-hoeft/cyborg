using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging;

/// <summary>
/// Stateless bridge from one REPL input line to the debugger command router. Command state is dispatch-local and supplied to CAF through DI.
/// </summary>
internal sealed class DebugCommandDispatcher(IDebugReplIo io, CafDebugCommandRouter router)
{
    internal async ValueTask<DebugResumeAction?> DispatchAsync(string commandLine, IDebugPauseContext context, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!CommandLineTokenizer.TryTokenize(commandLine, out string[] arguments, out string? error))
        {
            await io.WriteLineAsync(error!, OutputKind.Error, cancellationToken);
            return null;
        }
        if (arguments.Length == 0)
        {
            return null;
        }

        DebugCommandResult result = new();
        DebugCommandServiceProvider services = new(context, result, io);
        await router.RunAsync(arguments, services, io, cancellationToken).ConfigureAwait(false);
        return result.ResumeAction;
    }
}
