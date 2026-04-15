using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Configuration.Serialization.Dynamics;
using System.Text.Json;

namespace Cyborg.Core.Configuration.Model;

public sealed class DynamicValueJsonConverter(IDynamicValueProviderRegistry registry, IJsonLoaderContextProvider provider) : ExtendedJsonConverter<DynamicValue>(provider)
{
    public override DynamicValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException($"Expected property name token for dynamic value type, but got {reader.TokenType}.");
        }

        string typeName = reader.GetString() ?? throw new JsonException("Expected non-null string for dynamic value type.");

        if (!reader.Read())
        {
            throw new JsonException($"Expected value token for dynamic value type '{typeName}'.");
        }

        DynamicValue result = DynamicValueDeserializer.ReadValue(typeName, ref reader, registry, Context);

        if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException($"Expected end of dynamic value object, but got {reader.TokenType}.");
        }

        return result;
    }
}