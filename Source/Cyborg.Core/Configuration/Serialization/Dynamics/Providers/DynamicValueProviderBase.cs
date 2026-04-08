using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Configuration.Serialization.Dynamics;
using System.Text.Json;

namespace Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

public abstract class DynamicValueProviderBase(string typeName) : IDynamicValueProvider
{
    public string TypeName => typeName;

    protected static bool TryCreateValue<T>(T? nullable, [NotNullWhen(true)] out DynamicValue? value) where T : class
    {
        if (nullable is null)
        {
            value = null;
            return false;
        }
        value = new DynamicValue(nullable);
        return true;
    }

    public abstract bool TryCreateValue(ref Utf8JsonReader reader, IJsonLoaderContext context, [NotNullWhen(true)] out DynamicValue? value);
}
