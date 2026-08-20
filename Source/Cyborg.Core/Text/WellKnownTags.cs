namespace Cyborg.Core.Text;

/// <summary>
/// Well-known tag names that Cyborg interprets globally.
/// </summary>
public static class WellKnownTags
{
    /// <summary>
    /// Marks a string as a secret. Renderers redact values carrying this tag.
    /// </summary>
    public const string Secret = "cyborg.secret.v1";
}
