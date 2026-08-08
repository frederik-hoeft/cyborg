using Cyborg.Core.Modules.Debugging;
using System.Runtime.InteropServices;

namespace Cyborg.Cli.Debugging;

/// <summary>
/// Console-based debug frontend. The frontend owns REPL lifecycle and I/O while command dispatch and grammar are separate services.
/// </summary>
internal sealed class ConsoleDebugFrontend(IDebugReplIo io, DebugCommandDispatcher commandDispatcher) : IDebugFrontend
{
    private const string PROMPT = "(cyborg-dbg) ";

    public string Key => "console";

    public async ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await io.WriteLineAsync(string.Empty, OutputKind.Text, cancellationToken);
        OutputKind titleKind = context.ValidationResult.IsValid ? OutputKind.Status : OutputKind.Error;
        string errorLabel = context.ValidationResult.Errors.Count == 1 ? "error" : "errors";
        string validationSuffix = context.ValidationResult.IsValid ? string.Empty : $" [validation failed: {context.ValidationResult.Errors.Count} {errorLabel}]";
        await io.WriteLineAsync($"Breakpoint hit: {context.GetModuleIdentity()}{validationSuffix}", titleKind, cancellationToken);
        await DebugValidationOutput.WriteErrorsAsync(io, context.ValidationResult.Errors, cancellationToken);
        await io.WriteLineAsync("Type 'help' for available commands.", OutputKind.Status, cancellationToken);
        await io.WriteLineAsync(string.Empty, OutputKind.Text, cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await io.ReadLineAsync(PROMPT, cancellationToken).ConfigureAwait(false);
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

            DebugResumeAction? action = await commandDispatcher.DispatchAsync(line, context, cancellationToken).ConfigureAwait(false);
            if (action is not null)
            {
                return action.Value;
            }
        }
    }
}
