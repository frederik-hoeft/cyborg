namespace Cyborg.Core.Text.Rendering;

/// <summary>
/// Renders a <see cref="TaggedString"/> for logs, debugger inspection, and other display surfaces.
/// Implementations are resolved through DI so additional tags can contribute rendering policy.
/// </summary>
public interface ITaggedStringRenderer
{
    string Render(TaggedString value);
}
