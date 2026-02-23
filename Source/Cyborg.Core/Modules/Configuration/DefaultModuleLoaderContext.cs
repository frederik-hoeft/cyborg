using Cyborg.Core.Modules.Configuration.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Modules.Configuration;

public sealed class DefaultModuleLoaderContext(IServiceProvider serviceProvider, JsonSerializerContext jsonSerializerContext, IEnumerable<JsonConverter> jsonConverters) : IModuleLoaderContext
{
    public IServiceProvider ServiceProvider { get; } = serviceProvider;

    public JsonSerializerOptions JsonSerializerOptions
    {
        get
        {
            if (field is not null)
            {
                return field;
            }
            JsonSerializerOptions options = new(jsonSerializerContext.Options)
            {
                TypeInfoResolver = jsonSerializerContext,
            };
            foreach (JsonConverter converter in jsonConverters)
            {
                if (converter is IModuleJsonConverter moduleJsonConverter)
                {
                    moduleJsonConverter.Context = this;
                }
                options.Converters.Add(converter);
            }
            return field = options;
        }
    }
}
