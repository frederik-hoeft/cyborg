using Cyborg.Core.Modules.Configuration.Serialization;
using Cyborg.Core.Modules.Configuration.Serialization.Dynamics;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed class DynamicKeyValuePairJsonConverter(IDynamicValueProviderRegistry registry, IModuleLoaderContextProvider provider) : ModuleJsonConverter<DynamicKeyValuePair>(provider)
{
    public override DynamicKeyValuePair Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? key = null;
        object? value = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string propertyName = reader.GetString() ?? throw new JsonException("Expected non-null string for property name.");

            if (propertyName.Equals(nameof(DynamicKeyValuePair.Key), StringComparison.OrdinalIgnoreCase))
            {
                if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException("Expected string token for dynamic key-value pair key.");
                }

                key = reader.GetString();
                continue;
            }

            if (value is not null)
            {
                throw new JsonException($"Unexpected property '{propertyName}' in dynamic key-value pair.");
            }

            if (!reader.Read())
            {
                throw new JsonException($"Expected value token for dynamic value type '{propertyName}'.");
            }

            DynamicValue dynamicValue = DynamicValueDeserializer.ReadValue(propertyName, ref reader, registry, Context);
            value = dynamicValue.Value;
        }

        _ = key ?? throw new JsonException("Missing required property 'Key' in dynamic key-value pair.");
        _ = value ?? throw new JsonException("Missing dynamic value in dynamic key-value pair.");
        return new DynamicKeyValuePair(key, value);
    }
}