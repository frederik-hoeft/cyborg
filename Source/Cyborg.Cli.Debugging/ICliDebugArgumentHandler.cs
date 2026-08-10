using Cyborg.Core.Configuration.Builders;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli.Debugging;

/// <summary>
/// Integrates process-level debugger arguments with the host configuration and breakpoint session.
/// </summary>
public interface ICliDebugArgumentHandler
{
    string FrontendConfigurationKey { get; }

    bool TryConfigure(IConfigurationBuilder configurationBuilder, string[]? breakAt, [NotNullWhen(false)] out string? invalidExpression,
        [NotNullWhen(false)] out string? errorMessage);

    bool HasUsableFrontend();
}
