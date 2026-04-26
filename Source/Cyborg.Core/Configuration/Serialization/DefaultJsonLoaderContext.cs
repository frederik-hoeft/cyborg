using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Cyborg.Core.Configuration.Serialization;

public sealed class DefaultJsonLoaderContext
(
    IServiceProvider serviceProvider,
    IEnumerable<JsonSerializerContext> jsonSerializerContexts,
    IEnumerable<JsonConverter> jsonConverters
) : IJsonLoaderContext
{
    private readonly JsonSerializerContext[] _jsonSerializerContexts = [.. jsonSerializerContexts];
    private readonly JsonConverter[] _jsonConverters = [.. jsonConverters];

    public IServiceProvider ServiceProvider { get; } = serviceProvider;

    public JsonSerializerOptions JsonSerializerOptions
    {
        get
        {
            if (field is not null)
            {
                return field;
            }
            JsonSerializerOptions options = CreateOptions();
            foreach (JsonConverter converter in _jsonConverters)
            {
                options.Converters.Add(converter);
            }
            return field = options;
        }
    }

    private JsonSerializerOptions CreateOptions()
    {
        JsonSerializerContext? primaryContext = _jsonSerializerContexts.FirstOrDefault();

        JsonSerializerOptions options = primaryContext is not null
            ? new JsonSerializerOptions(primaryContext.Options)
            : new JsonSerializerOptions();

        foreach (JsonSerializerContext context in _jsonSerializerContexts.Skip(1))
        {
            if (!AreCompatible(options, context.Options))
            {
                throw new InvalidOperationException(
                    $"JsonSerializerContext '{context.GetType().FullName}' uses incompatible JsonSerializerOptions.");
            }
        }

        options.TypeInfoResolver = _jsonSerializerContexts switch
        {
            [] => options.TypeInfoResolver,
            [{ } single] => single,
            _ => JsonTypeInfoResolver.Combine([.. _jsonSerializerContexts.Cast<IJsonTypeInfoResolver>()])
        };

        return options;
    }

    private static bool AreCompatible(JsonSerializerOptions left, JsonSerializerOptions right) =>
        left.PropertyNamingPolicy == right.PropertyNamingPolicy
        && left.DictionaryKeyPolicy == right.DictionaryKeyPolicy
        && left.DefaultIgnoreCondition == right.DefaultIgnoreCondition
        && left.PropertyNameCaseInsensitive == right.PropertyNameCaseInsensitive
        && left.NumberHandling == right.NumberHandling
        && left.UnmappedMemberHandling == right.UnmappedMemberHandling;
}
