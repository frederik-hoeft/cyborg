using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Serialization;
using System.Text.Json;

namespace Cyborg.Core.Configuration;

public sealed class DefaultConfigurationLoader(IModuleLoaderContext configurationContext) : IConfigurationLoader
{
    public async Task AddSourceAsync(IConfiguration configuration, string configurationFilePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(configurationFilePath);

        await using FileStream stream = File.OpenRead(configurationFilePath);
        ConfigurationSource? source = await JsonSerializer.DeserializeAsync<ConfigurationSource>(stream, configurationContext, cancellationToken);
        _ = source ?? throw new InvalidOperationException($"Failed to deserialize configuration from file: {configurationFilePath}");
        configuration.AddSource(source);
    }
}