using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed class ModuleContextJsonConverter : JsonConverter<ModuleContext>
{
    private readonly ModuleRegistrySeedBuilder? _seedBuilder;

    public ModuleContextJsonConverter()
    {
    }

    internal ModuleContextJsonConverter(ModuleRegistrySeedBuilder seedBuilder)
    {
        ArgumentNullException.ThrowIfNull(seedBuilder);
        _seedBuilder = seedBuilder;
    }

    public override ModuleContext Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        JsonTypeInfo<ModuleContextDeserializationDummy> typeInfo = (JsonTypeInfo<ModuleContextDeserializationDummy>)options.GetTypeInfo(typeof(ModuleContextDeserializationDummy));
        ModuleContextDeserializationDummy result = JsonSerializer.Deserialize(ref reader, typeInfo)
            ?? throw new JsonException("Failed to deserialize ModuleContext.");
        if (result.Module.Definition.Name is { Length: > 0 } name)
        {
            _seedBuilder?.Add(name, result);
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, ModuleContext value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Serialization of modules is not supported.");
}

public sealed record ModuleContextDeserializationDummy
(
    ModuleReference Module,
    ModuleEnvironment Environment,
    ModuleReference? Configuration,
    ModuleRequirements Requires
) : ModuleContext(Module, Environment, Configuration, Requires);
