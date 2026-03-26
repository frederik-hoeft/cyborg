using Cyborg.Core.Modules.Configuration.Model;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

public abstract class DynamicValueProviderBase<T>(string typeName) : DynamicValueProviderBase(typeName) where T : class
{
    public override bool TryCreateValue(ref Utf8JsonReader reader, IModuleLoaderContext context, [NotNullWhen(true)] out DynamicValue? value)
    {
        T? deserializedValue = JsonSerializer.Deserialize<T>(ref reader, context);
        return TryCreateValue(deserializedValue, out value);
    }
}