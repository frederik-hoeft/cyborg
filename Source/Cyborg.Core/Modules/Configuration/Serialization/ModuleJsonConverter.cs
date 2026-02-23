using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Modules.Configuration.Serialization;

public abstract class ModuleJsonConverter<T> : JsonConverter<T>, IModuleJsonConverter
{
    internal protected IModuleLoaderContext Context
    {
        get => field ?? throw new InvalidOperationException("ModuleJsonConverter requires a configuration context to be set before deserialization can occur. Ensure that the converter is properly initialized with a valid IModuleConfigurationContext instance.");
        internal set => field = value;
    }

    IModuleLoaderContext IModuleJsonConverter.Context { get => Context; set => Context = value; }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Serialization of modules is not supported.");
}