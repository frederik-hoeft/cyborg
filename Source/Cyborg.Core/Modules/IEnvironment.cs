using Cyborg.Core.Aot.Json.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jab;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Cyborg.Core.Modules;

public interface IEnvironment
{
    string Name { get; }

    bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value);

    void SetVariable<T>(string name, T value);

    bool TryRemoveVariable(string name);
}

public interface IModule
{
    string Name { get; }

    Task<bool> ExecuteAsync(IEnvironment environment, CancellationToken cancellationToken);
}

public interface IModuleLoader
{
    string Name { get; }

    bool TryLoadModule(ref Utf8JsonReader reader, IModuleLoaderContext context, [NotNullWhen(true)] out IModule? module);
}

public interface IModuleLoaderRegistry
{
    bool TryGetModuleLoader(string name, [NotNullWhen(true)] out IModuleLoader? moduleLoader);
}

public sealed class DefaultModuleLoaderRegistry(IEnumerable<IModuleLoader> moduleLoaders) : IModuleLoaderRegistry
{
    private readonly FrozenDictionary<string, IModuleLoader> _configurations = moduleLoaders.ToFrozenDictionary(config => config.Name, StringComparer.OrdinalIgnoreCase);

    public bool TryGetModuleLoader(string name, [NotNullWhen(true)] out IModuleLoader? moduleLoader) => _configurations.TryGetValue(name, out moduleLoader);
}

public interface IModuleLoaderContext
{
    IServiceProvider ServiceProvider { get; }

    JsonSerializerOptions JsonSerializerOptions { get; }
}

public interface IConfigurationLoader
{
    Task<IModule> LoadMainModuleAsync(string configurationFilePath, CancellationToken cancellationToken);
}

public sealed class DefaultConfigurationLoader(IModuleLoaderContext configurationContext) : IConfigurationLoader
{
    public async Task<IModule?> LoadMainModuleAsync(string configurationFilePath, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(configurationFilePath);
        ModuleReference? mainModule = await JsonSerializer.DeserializeAsync<ModuleReference>(stream, configurationContext, cancellationToken);
        return mainModule?.Module;
    }
}

[JsonTypeInfoBindingsGenerator(GenerationMode = BindingsGenerationMode.Optimized)]
[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(MainModuleConfiguration))]
[JsonSerializable(typeof(SubprocessModuleConfiguration))]
public sealed partial class ModuleJsonSerializerContext : AotJsonSerializerContext;

[ServiceProviderModule]
[Singleton<ModuleJsonSerializerContext>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<JsonSerializerContext>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<IJsonTypeInfoProvider>(Factory = nameof(GetModuleJsonSerializerContext))]
[Singleton<IModuleLoaderContext, DefaultModuleLoaderContext>]
[Singleton<JsonConverter, ModuleReferenceJsonConverter>]
[Singleton<IModuleLoaderRegistry, DefaultModuleLoaderRegistry>]
[Singleton<IModuleLoader, MainModuleLoader>]
[Singleton<IModuleLoader, SubprocessModuleLoader>]
[Singleton<IConfigurationLoader, DefaultConfigurationLoader>]
public interface ICyborgCoreModule
{
    public static ModuleJsonSerializerContext GetModuleJsonSerializerContext() => ModuleJsonSerializerContext.Default;
}

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

public static class JsonSerializerExtensions
{
    extension (JsonSerializer)
    {
        [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
        [SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
        public static ValueTask<TValue?> DeserializeAsync<TValue>(Stream utf8Json, IModuleLoaderContext context, CancellationToken cancellationToken = default) =>
            JsonSerializer.DeserializeAsync<TValue>(utf8Json, context.JsonSerializerOptions, cancellationToken);

        [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
        [SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
        public static TValue? Deserialize<TValue>(ref Utf8JsonReader reader, IModuleLoaderContext context) =>
            JsonSerializer.Deserialize<TValue>(ref reader, context.JsonSerializerOptions);
    }
}

public sealed record ModuleReference([property: JsonIgnore] IModule Module);

public interface IModuleJsonConverter
{
    internal protected IModuleLoaderContext Context { get; internal set; }
}

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
        if (!configuration.TryLoadModule(ref reader, Context, out IModule? module))
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

public sealed record MainModuleConfiguration(ImmutableArray<ModuleReference> Steps);

public sealed class MainModule(string name, MainModuleConfiguration configuration) : IModule
{
    public string Name => name;

    public async Task<bool> ExecuteAsync(IEnvironment environment, CancellationToken cancellationToken)
    {
        foreach (ModuleReference step in configuration.Steps)
        {
            bool success = await step.Module.ExecuteAsync(environment, cancellationToken).ConfigureAwait(false);
            if (!success)
            {
                return false;
            }
        }
        return true;
    }
}

public sealed class MainModuleLoader : ModuleLoader<MainModule, MainModuleConfiguration>
{
    public override string Name => "cyborg.modules.main.v1";

    protected override MainModule CreateModule(MainModuleConfiguration configuration) => new(Name, configuration);
}

public abstract class ModuleLoader<TModule, TConfiguration> : IModuleLoader where TModule : class, IModule
{
    public abstract string Name { get; }

    public virtual bool TryLoadModule(ref Utf8JsonReader reader, IModuleLoaderContext context, [NotNullWhen(true)] out IModule? module)
    {
        TConfiguration? configuration = JsonSerializer.Deserialize<TConfiguration>(ref reader, context);
        if (configuration is not null)
        {
            module = CreateModule(configuration);
            return true;
        }
        module = null;
        return false;
    }

    protected abstract TModule CreateModule(TConfiguration configuration);
}

public sealed record SubprocessModuleConfiguration(string Executable, ImmutableArray<string> Arguments);

public sealed class SubprocessModule(string name, SubprocessModuleConfiguration configuration) : IModule
{
    public string Name => name;

    public async Task<bool> ExecuteAsync(IEnvironment environment, CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo(configuration.Executable, configuration.Arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.Start();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode == 0;
    }
}

public sealed class SubprocessModuleLoader : ModuleLoader<SubprocessModule, SubprocessModuleConfiguration>
{
    public override string Name => "cyborg.modules.subprocess.v1";

    protected override SubprocessModule CreateModule(SubprocessModuleConfiguration configuration) => new(Name, configuration);
}