using Cyborg.Core.Configuration.Model;
using System.Text.Json;

namespace Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

public sealed class CollectionDynamicValueProvider(IDynamicValueProvider itemProvider) : IDynamicValueProvider
{
    public string TypeName => "collection";

    public bool TryCreateValue(ref Utf8JsonReader reader, IJsonLoaderContext context, [NotNullWhen(true)] out DynamicValue? value)
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
