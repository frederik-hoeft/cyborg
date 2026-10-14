using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Runtime.Model;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Cyborg.Core.Runtime.Configuration;

internal sealed class ModuleConfigurationDeserializer(IJsonLoaderContext context)
{
    public async ValueTask<ModuleConfigurationLoadResult?> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default, ModuleOrigin? origin = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        (JsonSerializerOptions options, ModuleRegistrySeedBuilder seedBuilder) = CreateLoadState(origin);
        JsonTypeInfo<ModuleContext> typeInfo = (JsonTypeInfo<ModuleContext>)options.GetTypeInfo(typeof(ModuleContext));
        ModuleContext? moduleContext = await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken);
        return CreateResult(moduleContext, seedBuilder);
    }

    public ModuleConfigurationLoadResult? Deserialize(string json, ModuleOrigin? origin = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        (JsonSerializerOptions options, ModuleRegistrySeedBuilder seedBuilder) = CreateLoadState(origin);
        JsonTypeInfo<ModuleContext> typeInfo = (JsonTypeInfo<ModuleContext>)options.GetTypeInfo(typeof(ModuleContext));
        ModuleContext? moduleContext = JsonSerializer.Deserialize(json, typeInfo);
        return CreateResult(moduleContext, seedBuilder);
    }

    public ModuleReference DeserializeReference(string json, ModuleOrigin? origin = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        (JsonSerializerOptions options, _) = CreateLoadState(origin);
        JsonTypeInfo<ModuleReference> typeInfo = (JsonTypeInfo<ModuleReference>)options.GetTypeInfo(typeof(ModuleReference));
        return JsonSerializer.Deserialize(json, typeInfo) ?? throw new InvalidOperationException("Module reference deserialization returned null.");
    }

    private (JsonSerializerOptions Options, ModuleRegistrySeedBuilder SeedBuilder) CreateLoadState(ModuleOrigin? origin)
    {
        ModuleRegistrySeedBuilder seedBuilder = new();
        JsonSerializerOptions options = new(context.JsonSerializerOptions);
        for (int i = options.Converters.Count - 1; i >= 0; i--)
        {
            if (options.Converters[i] is ModuleContextJsonConverter)
            {
                options.Converters.RemoveAt(i);
            }
            else if (options.Converters[i] is ModuleReferenceJsonConverter referenceConverter)
            {
                options.Converters[i] = referenceConverter.WithOrigin(origin);
            }
        }
        options.Converters.Insert(0, new ModuleContextJsonConverter(seedBuilder));
        return (options, seedBuilder);
    }

    private static ModuleConfigurationLoadResult? CreateResult(ModuleContext? moduleContext, ModuleRegistrySeedBuilder seedBuilder) =>
        moduleContext is null ? null : new ModuleConfigurationLoadResult(moduleContext, seedBuilder.Build());
}
