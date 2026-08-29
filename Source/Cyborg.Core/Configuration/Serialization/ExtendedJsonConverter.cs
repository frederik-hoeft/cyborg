using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Configuration.Serialization;

public abstract class ExtendedJsonConverter<T>(IJsonLoaderContextProvider provider) : JsonConverter<T>
{
    protected IJsonLoaderContext GetContext(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        IJsonLoaderContext context = provider.GetContext();
        return ReferenceEquals(context.JsonSerializerOptions, options)
            ? context
            : new JsonLoaderContextView(context.ServiceProvider, options);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Serialization of modules is not supported.");

    private sealed class JsonLoaderContextView(IServiceProvider serviceProvider, JsonSerializerOptions jsonSerializerOptions) : IJsonLoaderContext
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public JsonSerializerOptions JsonSerializerOptions { get; } = jsonSerializerOptions;
    }
}
