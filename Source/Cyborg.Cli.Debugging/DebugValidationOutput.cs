using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Cli.Debugging;

internal static class DebugValidationOutput
{
    public static async ValueTask WriteErrorsAsync(IDebugReplIo io, IReadOnlyList<ValidationError> errors, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            return;
        }

        await io.WriteLineAsync("Validation errors:", OutputKind.Error, cancellationToken);
        foreach (ValidationError error in errors)
        {
            await io.WriteLineAsync($"  - {error.PropertyName} [{error.Rule}]: {error.Message}", OutputKind.Error, cancellationToken);
        }
    }
}
