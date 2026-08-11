using Cyborg.Core.Configuration.Model;
using System.Text.Json;

namespace Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

public abstract class DynamicEnumValueProvider<TEnum>(string typeName) : DynamicValueProviderBase(typeName) where TEnum : struct, Enum
{
    public override bool TryCreateValue(ref Utf8JsonReader reader, IJsonLoaderContext context, [NotNullWhen(true)] out DynamicValue? value)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            value = null;
            return false;
        }

        string? serializedValue = reader.GetString();
        if (serializedValue is null)
        {
            value = null;
            return false;
        }

        JsonNamingPolicy namingPolicy = context.JsonSerializerOptions.PropertyNamingPolicy ?? JsonNamingPolicy.SnakeCaseLower;
        foreach (TEnum candidate in Enum.GetValues<TEnum>())
        {
            string name = candidate.ToString();
            string configuredName = namingPolicy.ConvertName(name);
            if (string.Equals(serializedValue, configuredName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(serializedValue, name, StringComparison.OrdinalIgnoreCase))
            {
                value = new DynamicValue(candidate);
                return true;
            }
        }

        value = null;
        return false;
    }
}
