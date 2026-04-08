using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Configuration.Serialization;

public abstract class ExtendedJsonConverter<T>(IJsonLoaderContextProvider provider) : JsonConverter<T>
{
    protected IJsonLoaderContext Context => provider.GetContext();

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Serialization of modules is not supported.");
}