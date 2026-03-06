using Cyborg.Core.Modules.Configuration.Serialization;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed class DynamicValueJsonConverter(IDynamicValueProviderRegistry registry) : ModuleJsonConverter<DynamicValue>
{
    public override DynamicValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // read the current key (module name), resolve through the registry, and let the configuration loader handle the rest of the deserialization
        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException($"Expected string token for dynamic value type, but got {reader.TokenType}.");
        }
        string? typeName = reader.GetString() ?? throw new JsonException("Expected non-null string for dynamic value type.");
        if (!registry.TryGetProvider(typeName, out IDynamicValueProvider? provider))
        {
            throw new JsonException($"No dynamic value provider found for type '{typeName}'.");
        }
        if (!provider.TryCreateValue(ref reader, Context, out DynamicValue? value))
        {
            throw new JsonException($"Failed to create dynamic value for type '{typeName}'.");
        }
        return value;
    }

    public override void Write(Utf8JsonWriter writer, DynamicValue value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Serialization of DynamicValue is not supported.");
}