using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration.Serialization;

[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
    Justification = "This is safe, since we ensure that the JsonSerializerOptions is using the source-generated context, which will preserve the necessary metadata for the types being deserialized.")]
[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
    Justification = "This is safe, since we ensure that the JsonSerializerOptions is using the source-generated context, which will preserve the necessary metadata for the types being deserialized.")]
[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class JsonSerializerExtensions
{
    extension (JsonSerializer)
    {
        public static ValueTask<TValue?> DeserializeAsync<TValue>(Stream utf8Json, IModuleLoaderContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            return JsonSerializer.DeserializeAsync<TValue>(utf8Json, context.JsonSerializerOptions, cancellationToken);
        }

        public static TValue? Deserialize<TValue>(ref Utf8JsonReader reader, IModuleLoaderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return JsonSerializer.Deserialize<TValue>(ref reader, context.JsonSerializerOptions);
        }
    }
}
