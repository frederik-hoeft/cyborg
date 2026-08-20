using Cyborg.Core.Text;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Configuration.Serialization;

/// <summary>
/// Accepts a JSON string (untagged) or a structural <c>{ "value": "...", "tags": ["..."] }</c> object.
/// </summary>
public sealed class TaggedStringJsonConverter : JsonConverter<TaggedString>
{
    private const string VALUE_PROPERTY = "value";
    private const string TAGS_PROPERTY = "tags";

    public override TaggedString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => new TaggedString(reader.GetString()),
            JsonTokenType.Null => default,
            JsonTokenType.StartObject => ReadStructural(ref reader),
            _ => throw new JsonException($"Unexpected token '{reader.TokenType}' when reading {nameof(TaggedString)}. Expected a string or an object with '{VALUE_PROPERTY}' and optional '{TAGS_PROPERTY}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, TaggedString value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (!value.HasTags)
        {
            writer.WriteStringValue(value.Value);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString(VALUE_PROPERTY, value.Value);
        writer.WritePropertyName(TAGS_PROPERTY);
        writer.WriteStartArray();
        foreach (string tag in value.Tags.OrderBy(static t => t, StringComparer.Ordinal))
        {
            writer.WriteStringValue(tag);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static TaggedString ReadStructural(ref Utf8JsonReader reader)
    {
        string? value = null;
        bool hasValue = false;
        ImmutableHashSet<string>.Builder? tags = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (!hasValue)
                {
                    throw new JsonException($"Tagged string object must contain a '{VALUE_PROPERTY}' property.");
                }
                return new TaggedString(value, tags);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Unexpected token '{reader.TokenType}' inside tagged string object.");
            }

            string propertyName = reader.GetString() ?? string.Empty;
            if (!reader.Read())
            {
                throw new JsonException("Unexpected end of tagged string object.");
            }

            if (propertyName.Equals(VALUE_PROPERTY, StringComparison.OrdinalIgnoreCase))
            {
                if (reader.TokenType is not JsonTokenType.String and not JsonTokenType.Null)
                {
                    throw new JsonException($"Property '{VALUE_PROPERTY}' must be a string.");
                }
                value = reader.GetString();
                hasValue = true;
                continue;
            }

            if (propertyName.Equals(TAGS_PROPERTY, StringComparison.OrdinalIgnoreCase))
            {
                tags = ReadTags(ref reader);
                continue;
            }

            throw new JsonException($"Unexpected property '{propertyName}' on tagged string object. Expected '{VALUE_PROPERTY}' or '{TAGS_PROPERTY}'.");
        }

        throw new JsonException("Unexpected end of tagged string object.");
    }

    private static ImmutableHashSet<string>.Builder ReadTags(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Property '{TAGS_PROPERTY}' must be an array of strings.");
        }

        ImmutableHashSet<string>.Builder tags = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return tags;
            }
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Tag values must be strings, but found '{reader.TokenType}'.");
            }
            string? tag = reader.GetString();
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new JsonException("Tags must be non-empty strings.");
            }
            tags.Add(tag);
        }

        throw new JsonException("Unexpected end of tagged string tags array.");
    }
}
