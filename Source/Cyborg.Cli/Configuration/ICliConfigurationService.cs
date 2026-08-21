using Cyborg.Core.Configuration.Builders;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli.Configuration;

internal interface ICliConfigurationService
{
    bool TryConfigure(
        IConfigurationBuilder configurationBuilder,
        string optionsFilePath,
        string[]? configurationEntries,
        [NotNullWhen(false)] out string? errorMessage);
}
