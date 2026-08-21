using Cyborg.Cli.Arguments;
using Cyborg.Core.Configuration.Builders;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli.Configuration;

internal sealed class CliConfigurationService(IConfigurationArgumentHandler argumentHandler) : ICliConfigurationService
{
    public bool TryConfigure(
        IConfigurationBuilder configurationBuilder,
        string optionsFilePath,
        string[]? configurationEntries,
        [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        ArgumentException.ThrowIfNullOrEmpty(optionsFilePath);

        configurationBuilder.AddDictionary(CliConfigurationDefaults.Values);
        configurationBuilder.AddFiles(files => files.Add(optionsFilePath));
        return argumentHandler.TryProcessArgument(configurationEntries, configurationBuilder, out errorMessage);
    }
}
