using Cyborg.Core.Modules.Configuration.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed partial class DynamicValueJsonConverter(IDynamicValueProviderRegistry registry, IModuleLoaderContextProvider provider) : ModuleJsonConverter<DynamicValue>(provider)
{
    [GeneratedRegex(@"^collection<(?<provider>.*?)>$")]
    private static partial Regex CollectionRegex { get; }

    public override DynamicValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // read the current key (module name), resolve through the registry, and let the configuration loader handle the rest of the deserialization
        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException($"Expected string token for dynamic value type, but got {reader.TokenType}.");
        }
        string? propertyName = reader.GetString() ?? throw new JsonException("Expected non-null string for dynamic value type.");
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
            return new DynamicValue(items);
        }
        if (!provider.TryCreateValue(ref reader, Context, out DynamicValue? value))
        {
            throw new JsonException($"Failed to create dynamic value for type '{propertyName}'.");
        }
        return value;
    }
}