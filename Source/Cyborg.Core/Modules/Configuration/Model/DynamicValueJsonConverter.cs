using Cyborg.Core.Modules.Configuration.Serialization;
using Cyborg.Core.Modules.Configuration.Serialization.Dynamics;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed class DynamicValueJsonConverter(IDynamicValueProviderRegistry registry, IModuleLoaderContextProvider provider) : ModuleJsonConverter<DynamicValue>(provider)
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

        return DynamicValueDeserializer.ReadValue(typeName, ref reader, registry, Context);
    }
}