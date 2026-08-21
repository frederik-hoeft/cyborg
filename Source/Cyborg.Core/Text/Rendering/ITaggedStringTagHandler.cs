namespace Cyborg.Core.Text.Rendering;

/// <summary>
/// Applies display policy for one well-known or application-defined tag.
/// </summary>
/// <remarks>
/// Handlers receive only the display text produced so far. They deliberately do not receive the
/// raw <see cref="TaggedString"/> so a handler cannot recover a value that an earlier handler has
/// already redacted or otherwise hidden.
/// </remarks>
public interface ITaggedStringTagHandler
{
    string Tag { get; }

    string Render(string current);
}
