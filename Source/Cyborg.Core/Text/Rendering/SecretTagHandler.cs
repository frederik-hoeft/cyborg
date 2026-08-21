namespace Cyborg.Core.Text.Rendering;

public sealed class SecretTagHandler : ITaggedStringTagHandler
{
    public static string RedactedDisplay => "[REDACTED]";

    public string Tag => WellKnownTags.SECRET;

    public string Render(string current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return RedactedDisplay;
    }
}
