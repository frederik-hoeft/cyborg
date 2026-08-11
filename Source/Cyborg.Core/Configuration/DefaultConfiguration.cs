using Cyborg.Core.Configuration.Model;

namespace Cyborg.Core.Configuration;

public sealed class DefaultConfiguration : IConfiguration
{
    internal ConfigurationSources Sources { get; } = new();

    public bool IsFinalized { get; private set; }

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

    void IConfiguration.FinalizeWith(IEnumerable<IConfigurationSource> sources, IReadOnlySet<string> ignoredKeys)
    {
        if (IsFinalized)
        {
            throw new InvalidOperationException("Configuration has already been finalized.");
        }
        IsFinalized = true;
        foreach (IConfigurationSource source in sources)
        {
            Sources.AddSource(source, ignoredKeys);
        }
    }

    internal sealed class ConfigurationSources
    {
        public Dictionary<string, object?> Options = [];

        public void AddSource(IConfigurationSource source, IReadOnlySet<string> ignoredKeys)
        {
            foreach (DynamicKeyValuePair option in source.Options)
            {
                AddValue(parentKey: null, option, ignoredKeys);
            }
        }

        private void AddValue(string? parentKey, DynamicKeyValuePair property, IReadOnlySet<string> ignoredKeys)
        {
            string key = parentKey is null ? property.Key : $"{parentKey}.{property.Key}";
            if (ignoredKeys.Contains(key))
            {
                return;
            }
            if (property.Value is IDecomposable decomposable)
            {
                foreach (DynamicKeyValuePair nestedProperty in decomposable.Decompose())
                {
                    AddValue(key, nestedProperty, ignoredKeys);
                }
                return;
            }
            Options[key] = property.Value;
        }
    }
}
