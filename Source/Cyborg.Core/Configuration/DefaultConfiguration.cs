using Cyborg.Core.Configuration.Model;

namespace Cyborg.Core.Configuration;

public sealed class DefaultConfiguration : IConfiguration
{
    internal ConfigurationSources Sources { get; } = new();

    public object? this[string key] => Sources.Options.TryGetValue(key, out object? value) ? value : null;

    public bool TryGetValue<T>(string key, [NotNullWhen(true)] out T? value)
    {
        if (Sources.Options.TryGetValue(key, out object? objValue) && objValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    public T Get<T>(string key, Func<T> defaultProvider)
    {
        ArgumentNullException.ThrowIfNull(defaultProvider);
        if (TryGetValue(key, out T? value))
        {
            return value;
        }
        return defaultProvider.Invoke();
    }

    [return: NotNullIfNotNull(nameof(defaultValue))]
    public T? Get<T>(string key, T? defaultValue = default)
    {
        if (TryGetValue(key, out T? value))
        {
            return value;
        }
        return defaultValue;
    }

    void IConfiguration.AddSource(ConfigurationSource source) => Sources.AddSource(source);

    internal sealed class ConfigurationSources
    {
        public Dictionary<string, object?> Options = [];

        public void AddSource(ConfigurationSource context)
        {
            foreach (DynamicKeyValuePair option in context.Options)
            {
                AddValue(parentKey: null, option);
            }
        }

        private void AddValue(string? parentKey, DynamicKeyValuePair property)
        {
            string key = parentKey is null ? property.Key : $"{parentKey}:{property.Key}";
            Options[key] = property.Value;
            if (property.Value is IDecomposable decomposable)
            {
                foreach (DynamicKeyValuePair nestedProperty in decomposable.Decompose())
                {
                    AddValue(key, nestedProperty);
                }
            }
        }
    }
}
