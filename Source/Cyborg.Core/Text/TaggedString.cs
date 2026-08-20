using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Text.Rendering;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Text;

/// <summary>
/// A string value together with arbitrary metadata tags. Tags union across interpolation and
/// indirection so taint such as <see cref="WellKnownTags.Secret"/> cannot be stripped by composition.
/// </summary>
/// <remarks>
/// Use <see cref="Value"/> to read the raw string at an execution boundary. Cyborg-controlled
/// presentation paths should use <see cref="ITaggedStringRenderer"/>. <see cref="ToString"/> is a
/// conservative context-free fallback for callers that cannot access DI.
/// </remarks>
[JsonConverter(typeof(TaggedStringJsonConverter))]
[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.TaggedString)]
public readonly struct TaggedString : IEquatable<TaggedString>, IEquatable<string>
{
    private readonly ImmutableHashSet<string>? _tags;
    private readonly string? _value;

    /// <summary>
    /// The raw string value. This is the execution-facing surface and is never redacted.
    /// </summary>
    public string Value => _value ?? string.Empty;

    public ImmutableHashSet<string> Tags => _tags ?? ImmutableHashSet<string>.Empty;

    public bool HasTags => _tags is { Count: > 0 };

    public bool IsEmpty => Value.Length == 0 && !HasTags;

    public TaggedString(string? value, IEnumerable<string>? tags = null)
    {
        _value = value ?? string.Empty;
        _tags = NormalizeTags(tags);
    }

    public bool HasTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return Tags.Contains(tag);
    }

    public TaggedString WithValue(string? value) => new(value, Tags);

    public TaggedString WithTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return Tags.Contains(tag) ? this : new TaggedString(Value, Tags.Add(tag));
    }

    public TaggedString WithTags(IEnumerable<string>? tags)
    {
        ImmutableHashSet<string> merged = UnionTags(Tags, tags);
        return ReferenceEquals(merged, Tags) ? this : new TaggedString(Value, merged);
    }

    /// <summary>
    /// Returns a tagged string with this value and the union of this instance's tags and <paramref name="other"/>'s tags.
    /// The other value is not concatenated.
    /// </summary>
    public TaggedString UnionTags(TaggedString other) => WithTags(other.Tags);

    public static TaggedString Concat(TaggedString left, TaggedString right) =>
        new(left.Value + right.Value, UnionTags(left.Tags, right.Tags));

    public static TaggedString Concat(string? left, TaggedString right) =>
        new((left ?? string.Empty) + right.Value, right.Tags);

    public static TaggedString Concat(TaggedString left, string? right) =>
        new(left.Value + (right ?? string.Empty), left.Tags);

    public static implicit operator TaggedString(string? value) => new(value);

    public static explicit operator string(TaggedString value) => value.Value;

    public bool Equals(TaggedString other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal) && Tags.SetEquals(other.Tags);

    public bool Equals(string? other) => string.Equals(Value, other, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is TaggedString tagged && Equals(tagged);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Value, StringComparer.Ordinal);
        foreach (string tag in Tags.OrderBy(static tag => tag, StringComparer.Ordinal))
        {
            hash.Add(tag, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Returns a conservative context-free display representation. Cyborg presentation paths with
    /// access to dependency injection should use <see cref="ITaggedStringRenderer"/> instead.
    /// </summary>
    public override string ToString() => DefaultTaggedStringRenderer.ContextFreeFallback.Render(this);

    public static bool operator ==(TaggedString left, TaggedString right) => left.Equals(right);

    public static bool operator !=(TaggedString left, TaggedString right) => !left.Equals(right);

    public static bool operator ==(TaggedString left, string? right) => left.Equals(right);

    public static bool operator !=(TaggedString left, string? right) => !left.Equals(right);

    public static bool operator ==(string? left, TaggedString right) => right.Equals(left);

    public static bool operator !=(string? left, TaggedString right) => !right.Equals(left);

    internal static ImmutableHashSet<string> UnionTags(IEnumerable<string>? left, IEnumerable<string>? right)
    {
        ImmutableHashSet<string> leftTags = NormalizeTags(left) ?? ImmutableHashSet<string>.Empty;
        ImmutableHashSet<string> rightTags = NormalizeTags(right) ?? ImmutableHashSet<string>.Empty;
        if (rightTags.IsEmpty)
        {
            return leftTags;
        }
        if (leftTags.IsEmpty)
        {
            return rightTags;
        }
        return leftTags.Union(rightTags);
    }

    internal static ImmutableHashSet<string>? NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return null;
        }
        if (tags is ImmutableHashSet<string> existing)
        {
            return existing.Count == 0 ? null : existing.WithComparer(StringComparer.Ordinal);
        }

        ImmutableHashSet<string>.Builder builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (string tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("Tags must be non-empty.", nameof(tags));
            }
            builder.Add(tag);
        }
        return builder.Count == 0 ? null : builder.ToImmutable();
    }
}
