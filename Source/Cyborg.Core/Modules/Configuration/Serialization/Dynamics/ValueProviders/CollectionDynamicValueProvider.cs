using Cyborg.Core.Modules.Configuration.Model;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

public sealed class CollectionDynamicValueProvider(IDynamicValueProvider itemProvider) : IDynamicValueProvider
{
    public string TypeName => "collection";

    public bool TryCreateValue(ref Utf8JsonReader reader, IModuleLoaderContext context, [NotNullWhen(true)] out DynamicValue? value)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            value = null;
            return false;
        }

        List<object?> items = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (!itemProvider.TryCreateValue(ref reader, context, out DynamicValue? itemValue))
            {
                value = null;
                return false;
            }

            items.Add(itemValue.Value);
        }

        value = new DynamicValue(items);
        return true;
    }
}
