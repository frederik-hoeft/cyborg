using Cyborg.Core.Modules.Configuration.Serialization;
using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed class DynamicKeyValuePairJsonConverter(IDynamicValueProviderRegistry registry) : ModuleJsonConverter<DynamicKeyValuePair>
{
    public override DynamicKeyValuePair Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // read the current key (module name), resolve through the registry, and let the configuration loader handle the rest of the deserialization
        string? key = null;
        DynamicValue? value = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string propertyName = reader.GetString() ?? throw new JsonException("Expected non-null string for property name.");
            if (propertyName.Equals(nameof(DynamicKeyValuePair.Key), StringComparison.OrdinalIgnoreCase) && reader.Read() && reader.TokenType == JsonTokenType.String)
            {
                key = reader.GetString();
            }
            else if (value is null)
            {
                if (!registry.TryGetProvider(propertyName, out IDynamicValueProvider? provider))
                {
                    throw new JsonException($"No dynamic value provider found for type '{propertyName}'. Must be one of: {string.Join(", ", registry.GetRegisteredTypeNames())}.");
                }
                if (!reader.Read() || !provider.TryCreateValue(ref reader, Context, out value))
                {
                    throw new JsonException($"Failed to create dynamic value for type '{propertyName}'.");
                }
            }
            else
            {
                throw new JsonException($"Unexpected property '{propertyName}' in dynamic key-value pair.");
            }
        }
        _ = key ?? throw new JsonException("Missing required property 'Key' in dynamic key-value pair.");
        _ = value ?? throw new JsonException("Missing dynamic value in dynamic key-value pair.");
        return new DynamicKeyValuePair(key, value.Value);
    }

    public override void Write(Utf8JsonWriter writer, DynamicKeyValuePair value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Serialization of DynamicKeyValuePair is not supported.");
}