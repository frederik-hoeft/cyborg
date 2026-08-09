using Cyborg.Core.Configuration.Model;

namespace Cyborg.Core.Configuration.Loaders;

public sealed class ConfigurationDictionaryLoader : IConfigurationDictionaryLoader
{
    private readonly Dictionary<string, object?> _keyValuePairs = [];

    public IConfigurationDictionaryLoader AddEntry<T>(string key, T value)
    {
        _keyValuePairs[key] = value;
        return this;
    }

    public IAsyncEnumerable<IConfigurationSource> LoadSourcesAsync(CancellationToken cancellationToken)
    {
        IEnumerable<DynamicKeyValuePair> valuePairs = _keyValuePairs.Select(kvp => new DynamicKeyValuePair(kvp.Key, kvp.Value));
        return AsyncEnumerable.Repeat(new ConfigurationSource([.. valuePairs]), 1);
    }
}
