using System.Collections.Frozen;

namespace Cyborg.Core.Text.Rendering;

public sealed class DefaultTaggedStringRenderer : ITaggedStringRenderer
{
    internal static DefaultTaggedStringRenderer Fallback { get; } = new(new SecretTagHandler());

    private readonly FrozenDictionary<string, ITaggedStringTagHandler> _handlers;

    public DefaultTaggedStringRenderer(IEnumerable<ITaggedStringTagHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _handlers = handlers.ToFrozenDictionary(static handler => handler.Tag, StringComparer.Ordinal);
    }

    private DefaultTaggedStringRenderer(params ITaggedStringTagHandler[] handlers)
        : this((IEnumerable<ITaggedStringTagHandler>)handlers)
    {
    }

    public string Render(TaggedString value)
    {
        if (!value.HasTags)
        {
            return value.Value;
        }

        string current = value.Value;
        foreach (string tag in value.Tags.OrderBy(static t => t, StringComparer.Ordinal))
        {
            if (_handlers.TryGetValue(tag, out ITaggedStringTagHandler? handler))
            {
                current = handler.Render(value, current);
            }
        }
        return current;
    }
}
