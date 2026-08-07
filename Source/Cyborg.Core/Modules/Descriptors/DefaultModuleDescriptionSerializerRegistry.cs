using System.Collections.Frozen;

namespace Cyborg.Core.Modules.Descriptors;

internal sealed class DefaultModuleDescriptionSerializerRegistry : IModuleDescriptionSerializerRegistry
{
    private readonly FrozenDictionary<string, IModuleDescriptionSerializer> _serializers;

    internal DefaultModuleDescriptionSerializerRegistry(IEnumerable<IModuleDescriptionSerializer> serializers)
    {
        ArgumentNullException.ThrowIfNull(serializers);

        Dictionary<string, IModuleDescriptionSerializer> serializersByFormat = new(StringComparer.OrdinalIgnoreCase);
        foreach (IModuleDescriptionSerializer serializer in serializers)
        {
            ArgumentNullException.ThrowIfNull(serializer);
            string format = serializer.Format;
            ArgumentException.ThrowIfNullOrWhiteSpace(format);

            if (!serializersByFormat.TryAdd(format, serializer))
            {
                throw new InvalidOperationException($"More than one module description serializer is registered for format '{format}'.");
            }
        }

        _serializers = serializersByFormat.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public IModuleDescriptionSerializer GetRequiredSerializer(string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        return TryGetSerializer(format, out IModuleDescriptionSerializer? serializer)
            ? serializer
            : throw new KeyNotFoundException($"No module description serializer is registered for format '{format}'.");
    }

    public bool TryGetSerializer(string format, [NotNullWhen(true)] out IModuleDescriptionSerializer? serializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        return _serializers.TryGetValue(format, out serializer);
    }
}
