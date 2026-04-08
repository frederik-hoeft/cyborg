using Cyborg.Core.Configuration.Serialization;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed class ModuleContextJsonConverter(IModuleRegistry registry, IJsonLoaderContextProvider provider) : ExtendedJsonConverter<ModuleContext>(provider)
{
    public override ModuleContext Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ModuleContextDeserializationDummy result = JsonSerializer.Deserialize<ModuleContextDeserializationDummy>(ref reader, Context)
            ?? throw new JsonException("Failed to deserialize ModuleContext.");
        if (result.Module.Module.Module.Name is { Length: > 0 } name)
        {
            _ = registry.TryAddModule(name, result);
        }
        return result;
    }
}

public sealed record ModuleContextDeserializationDummy
(
    ModuleReference Module,
    ModuleEnvironment Environment,
    ModuleReference? Configuration,
    ModuleRequirements Requires
) : ModuleContext(Module, Environment, Configuration, Requires);