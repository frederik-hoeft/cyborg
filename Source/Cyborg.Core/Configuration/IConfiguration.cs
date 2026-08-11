namespace Cyborg.Core.Configuration;

/// <summary>
/// Host-level hierarchical configuration store. Stores leaf values only under dotted path keys.
/// </summary>
public interface IConfiguration : IHierarchicalKeyValueStore
{
    bool IsFinalized { get; }

    T Get<T>(string key, Func<T> defaultProvider);

    [return: NotNullIfNotNull(nameof(defaultValue))]
    T? Get<T>(string key, T? defaultValue = default);

    internal void FinalizeWith(IEnumerable<IConfigurationSource> sources, IReadOnlySet<string> ignoredKeys);
}
