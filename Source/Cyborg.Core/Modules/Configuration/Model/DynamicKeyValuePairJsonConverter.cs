using Cyborg.Core.Modules.Configuration.Serialization;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed partial class DynamicKeyValuePairJsonConverter(IDynamicValueProviderRegistry registry) : ModuleJsonConverter<DynamicKeyValuePair>
{
    [GeneratedRegex(@"^collection<(?<provider>.*?)>$")]
    private static partial Regex CollectionRegex { get; }

    public override DynamicKeyValuePair Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // read the current key (module name), resolve through the registry, and let the configuration loader handle the rest of the deserialization
        string? key = null;
        object? value = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string propertyName = reader.GetString() ?? throw new JsonException("Expected non-null string for property name.");
            if (propertyName.Equals(nameof(DynamicKeyValuePair.Key), StringComparison.OrdinalIgnoreCase) && reader.Read() && reader.TokenType == JsonTokenType.String)
            {
                key = reader.GetString();
            }
            else if (value is null && reader.Read())
            {
                if (!registry.TryGetProvider(propertyName, out IDynamicValueProvider? provider))
                {
                    if (reader.TokenType is not JsonTokenType.StartArray
                        || CollectionRegex.Match(propertyName) is not { Success: true } match
                        || !registry.TryGetProvider(match.Groups["provider"].Value, out IDynamicValueProvider? itemProvider))
                    {
                        throw new JsonException($"No dynamic value provider found for type '{propertyName}'. Must be one of: {string.Join(", ", registry.GetRegisteredTypeNames())}.");
                    }
                    // handle collection of dynamic values
                    List<object> items = [];
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (!itemProvider.TryCreateValue(ref reader, Context, out DynamicValue? itemValue))
                        {
                            throw new JsonException($"Failed to create dynamic value for type '{match.Groups["provider"].Value}' in collection.");
                        }
                        items.Add(itemValue.Value);
                    }
                    value = items;
                }
                else
                {
                    if (!provider.TryCreateValue(ref reader, Context, out DynamicValue? dynamicValue))
                    {
                        throw new JsonException($"Failed to create dynamic value for type '{propertyName}'.");
                    }
                    value = dynamicValue.Value;
                }
            }
            else
            {
                throw new JsonException($"Unexpected property '{propertyName}' in dynamic key-value pair.");
            }
        }
        _ = key ?? throw new JsonException("Missing required property 'Key' in dynamic key-value pair.");
        _ = value ?? throw new JsonException("Missing dynamic value in dynamic key-value pair.");
        return new DynamicKeyValuePair(key, value);
    }

    public override void Write(Utf8JsonWriter writer, DynamicKeyValuePair value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Serialization of DynamicKeyValuePair is not supported.");
}