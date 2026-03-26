using Cyborg.Core.Modules.Configuration.Model;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

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

    public abstract bool TryCreateValue(ref Utf8JsonReader reader, IModuleLoaderContext context, [NotNullWhen(true)] out DynamicValue? value);
}
