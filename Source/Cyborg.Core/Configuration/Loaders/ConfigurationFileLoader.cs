using Cyborg.Core.Configuration.Serialization;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Cyborg.Core.Configuration.Loaders;

public sealed class ConfigurationFileLoader(IJsonLoaderContext configurationContext) : IConfigurationFileLoader
{
    private readonly HashSet<string> _configFiles = [];

    public IConfigurationFileLoader Add(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _configFiles.Add(filePath);
        return this;
    }

    public async IAsyncEnumerable<IConfigurationSource> LoadSourcesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // snap a defensive copy of the config files to avoid issues with modifications during enumeration
        ImmutableArray<string> configFiles = [.. _configFiles];
        foreach (string configFile in configFiles)
        {
            await using FileStream stream = File.OpenRead(configFile);
            ConfigurationSource? source = await JsonSerializer.DeserializeAsync<ConfigurationSource>(stream, configurationContext, cancellationToken);
            _ = source ?? throw new InvalidOperationException($"Failed to deserialize configuration from file: {configFile}");
            yield return source;
        }
    }
}
