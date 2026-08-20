namespace Cyborg.Core.Text.Rendering;

public sealed class SecretTagHandler : ITaggedStringTagHandler
{
    public const string RedactedDisplay = "[REDACTED]";

    public string Tag => WellKnownTags.Secret;

    public string Render(string current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return RedactedDisplay;
    }
}
