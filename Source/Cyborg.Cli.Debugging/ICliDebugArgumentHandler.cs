using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli.Debugging;

/// <summary>
/// Integrates process-level breakpoint arguments with the debugger session and validates the selected frontend.
/// </summary>
public interface ICliDebugArgumentHandler
{
    string FrontendConfigurationKey { get; }

    bool TryConfigure(string[]? breakAt, [NotNullWhen(false)] out string? invalidExpression, [NotNullWhen(false)] out string? errorMessage);

    bool HasUsableFrontend();
}
