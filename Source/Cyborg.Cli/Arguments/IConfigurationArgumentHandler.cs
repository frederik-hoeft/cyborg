using Cyborg.Core.Configuration.Builders;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli.Arguments;

internal interface IConfigurationArgumentHandler
{
    bool TryProcessArgument(
        string[]? configurationEntries,
        IConfigurationBuilder configurationBuilder,
        [NotNullWhen(false)] out string? invalidDefinition,
        [NotNullWhen(false)] out string? errorMessage);
}
