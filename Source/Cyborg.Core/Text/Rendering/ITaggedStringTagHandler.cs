namespace Cyborg.Core.Text.Rendering;

/// <summary>
/// Applies display policy for a single well-known or application-defined tag.
/// Handlers are composed by <see cref="ITaggedStringRenderer"/> in tag-name order.
/// </summary>
public interface ITaggedStringTagHandler
{
    string Tag { get; }

    /// <summary>
    /// Transforms the current display text for a value that carries <see cref="Tag"/>.
    /// </summary>
    /// <param name="value">The tagged string being rendered.</param>
    /// <param name="current">The display text produced so far (initially the raw value).</param>
    string Render(TaggedString value, string current);
}
