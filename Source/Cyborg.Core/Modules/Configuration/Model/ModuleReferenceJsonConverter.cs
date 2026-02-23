using Cyborg.Core.Modules.Configuration.Serialization;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed class ModuleReferenceJsonConverter(IModuleLoaderRegistry registry) : ModuleJsonConverter<ModuleReference>
{
    public override ModuleReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // read the current key (module name), resolve through the registry, and let the configuration loader handle the rest of the deserialization
        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException($"Expected string token for module reference, but got {reader.TokenType}.");
        }
        string? moduleName = reader.GetString() ?? throw new JsonException("Module reference cannot be null. Ensure that the JSON configuration specifies a valid module name.");
        if (!registry.TryGetModuleLoader(moduleName, out IModuleLoader? configuration))
        {
            throw new JsonException($"Module configuration with name '{moduleName}' not found in the registry. Ensure that the module name specified in the JSON configuration matches a registered module configuration.");
        }
        if (!configuration.TryCreateModule(ref reader, Context, out IModuleWorker? module))
        {
            throw new JsonException($"Failed to create module instance for module '{moduleName}'. Ensure that the JSON configuration for the module is valid and that the module configuration implementation can handle it.");
        }
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException($"Expected end of module reference object after reading module configuration, but got {reader.TokenType}. Ensure that the JSON configuration for the module is properly formatted.");
        }
        return new ModuleReference(module);
    }

    public override void Write(Utf8JsonWriter writer, ModuleReference value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Serialization of ModuleReference is not supported.");
}
