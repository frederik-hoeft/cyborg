using Cyborg.Core.Configuration.Model;
using System.Text.Json;

namespace Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

public abstract class DynamicValueProviderBase<T>(string typeName) : DynamicValueProviderBase(typeName) where T : class
{
    public override bool TryCreateValue(ref Utf8JsonReader reader, IJsonLoaderContext context, [NotNullWhen(true)] out DynamicValue? value)
    {
        T? deserializedValue = JsonSerializer.Deserialize<T>(ref reader, context);
        return TryCreateValue(deserializedValue, out value);
    }
}