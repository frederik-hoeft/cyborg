namespace Cyborg.Core.Configuration;

public interface IConfiguration
{
    bool TryGetValue<T>(string key, [NotNullWhen(true)] out T? value);

    T Get<T>(string key, Func<T> defaultProvider);

    [return: NotNullIfNotNull(nameof(defaultValue))]
    T? Get<T>(string key, T? defaultValue = default);

    object? this[string key] { get; }

    internal void AddSource(ConfigurationSource source);
}