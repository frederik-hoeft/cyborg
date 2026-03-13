using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Modules.Configuration.Serialization;

public abstract class ModuleJsonConverter<T>(IModuleLoaderContextProvider provider) : JsonConverter<T>
{
    protected IModuleLoaderContext Context => provider.GetContext();

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Serialization of modules is not supported.");
}