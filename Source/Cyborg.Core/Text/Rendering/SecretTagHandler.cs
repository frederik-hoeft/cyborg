namespace Cyborg.Core.Text.Rendering;

public sealed class SecretTagHandler : ITaggedStringTagHandler
{
    public string Tag => WellKnownTags.Secret;

    public string Render(TaggedString value, string current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return TaggedString.RedactedDisplay;
    }
}
