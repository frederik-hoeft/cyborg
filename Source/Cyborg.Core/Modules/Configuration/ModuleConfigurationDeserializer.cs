using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Modules.Configuration.Model;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Cyborg.Core.Modules.Configuration;

internal sealed class ModuleConfigurationDeserializer(IJsonLoaderContext context)
{
    public async ValueTask<ModuleContext?> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        (JsonSerializerOptions Options, ModuleRegistrySeedBuilder SeedBuilder) load = CreateLoadState();
        JsonTypeInfo<ModuleContext> typeInfo = (JsonTypeInfo<ModuleContext>)load.Options.GetTypeInfo(typeof(ModuleContext));
        ModuleContext? moduleContext = await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken);
        return AttachSeed(moduleContext, load.SeedBuilder);
    }

    public ModuleContext? Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        (JsonSerializerOptions Options, ModuleRegistrySeedBuilder SeedBuilder) load = CreateLoadState();
        JsonTypeInfo<ModuleContext> typeInfo = (JsonTypeInfo<ModuleContext>)load.Options.GetTypeInfo(typeof(ModuleContext));
        ModuleContext? moduleContext = JsonSerializer.Deserialize(json, typeInfo);
        return AttachSeed(moduleContext, load.SeedBuilder);
    }

    private (JsonSerializerOptions Options, ModuleRegistrySeedBuilder SeedBuilder) CreateLoadState()
    {
        ModuleRegistrySeedBuilder seedBuilder = new();
        JsonSerializerOptions options = new(context.JsonSerializerOptions);
        for (int i = options.Converters.Count - 1; i >= 0; i--)
        {
            if (options.Converters[i] is ModuleContextJsonConverter)
            {
                options.Converters.RemoveAt(i);
            }
        }
        options.Converters.Insert(0, new ModuleContextJsonConverter(seedBuilder));
        return (options, seedBuilder);
    }

    private static ModuleContext? AttachSeed(ModuleContext? moduleContext, ModuleRegistrySeedBuilder seedBuilder) =>
        moduleContext is null ? null : moduleContext with { NamedModules = seedBuilder.Build() };
}
