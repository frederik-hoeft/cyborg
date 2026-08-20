using System.Collections.Frozen;

namespace Cyborg.Core.Text.Rendering;

public sealed class DefaultTaggedStringRenderer : ITaggedStringRenderer
{
    internal static DefaultTaggedStringRenderer SafeFallback { get; } = new(new SecretTagHandler());

    private readonly FrozenDictionary<string, ITaggedStringTagHandler> _handlers;

    public DefaultTaggedStringRenderer(IEnumerable<ITaggedStringTagHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        Dictionary<string, ITaggedStringTagHandler> handlersByTag = new(StringComparer.Ordinal);
        foreach (ITaggedStringTagHandler handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentException.ThrowIfNullOrWhiteSpace(handler.Tag);
            if (!handlersByTag.TryAdd(handler.Tag, handler))
            {
                throw new InvalidOperationException($"Multiple tagged-string render handlers are registered for tag '{handler.Tag}'.");
            }
        }

        _handlers = handlersByTag.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private DefaultTaggedStringRenderer(params ITaggedStringTagHandler[] handlers)
        : this((IEnumerable<ITaggedStringTagHandler>)handlers)
    {
    }

    public string Render(TaggedString value)
    {
        string current = value.Value;
        if (!value.HasTags)
        {
            return current;
        }

        foreach (string tag in value.Tags.OrderBy(static tag => tag, StringComparer.Ordinal))
        {
            if (_handlers.TryGetValue(tag, out ITaggedStringTagHandler? handler))
            {
                current = handler.Render(current);
            }
        }
        return current;
    }
}
