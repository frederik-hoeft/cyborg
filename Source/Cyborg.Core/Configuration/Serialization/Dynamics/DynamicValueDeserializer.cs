using Cyborg.Core.Configuration.Model;
using System.Text.Json;

namespace Cyborg.Core.Configuration.Serialization.Dynamics;

internal static class DynamicValueDeserializer
{
    public static DynamicValue ReadValue(string typeName, ref Utf8JsonReader reader, IDynamicValueProviderRegistry registry, IJsonLoaderContext context)
    {
        DynamicValueTypeReference typeReference = DynamicValueTypeReferenceParser.Parse(typeName);

        if (!registry.TryResolveProvider(typeReference, out IDynamicValueProvider? provider))
        {
            throw new JsonException(
                $"No dynamic value provider found for type '{typeReference}'. Must be one of: {string.Join(", ", registry.GetRegisteredTypeNames())}.");
        }

        if (!provider.TryCreateValue(ref reader, context, out DynamicValue? value))
        {
            throw new JsonException($"Failed to create dynamic value for type '{typeReference}'.");
        }

        return value;
    }
}
