using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Text;
using System.Text.Json;

namespace Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

public sealed class DynamicTaggedStringProvider() : DynamicValueProviderBase(WellKnownDynamicValueTypes.TaggedString)
{
    public override bool TryCreateValue(ref Utf8JsonReader reader, IJsonLoaderContext context, [NotNullWhen(true)] out DynamicValue? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        TaggedString tagged = JsonSerializer.Deserialize<TaggedString>(ref reader, context);
        value = new DynamicValue(tagged);
        return true;
    }
}
