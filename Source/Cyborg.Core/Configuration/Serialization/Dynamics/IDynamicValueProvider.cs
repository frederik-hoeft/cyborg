using Cyborg.Core.Configuration.Model;
using System.Text.Json;

namespace Cyborg.Core.Configuration.Serialization.Dynamics;

public interface IDynamicValueProvider
{
    string TypeName { get; }

    bool TryCreateValue(ref Utf8JsonReader reader, IJsonLoaderContext context, [NotNullWhen(true)] out DynamicValue? value);
}
