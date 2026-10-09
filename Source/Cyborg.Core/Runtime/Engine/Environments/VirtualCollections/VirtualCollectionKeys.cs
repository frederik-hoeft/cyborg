namespace Cyborg.Core.Runtime.Engine.Environments.VirtualCollections;

/// <summary>
/// Storage keys for virtual-collection markers and elements.
/// The keys are not valid variable references and are omitted from environment enumeration.
/// </summary>
internal static class VirtualCollectionKeys
{
    private const char SEPARATOR = '\u001F';
    private const string PREFIX = "\u001Fvc\u001F";
    private const string MARKER_TAIL = "d";
    private const string ELEMENT_TAIL = "e";

    public static bool IsInternal(string? name) =>
        name is not null && name.StartsWith(PREFIX, StringComparison.Ordinal);

    public static string Marker(string collectionName) =>
        string.Concat(PREFIX, collectionName, SEPARATOR, MARKER_TAIL);

    public static string Element(string collectionName, string elementId)
    {
        ArgumentException.ThrowIfNullOrEmpty(elementId);
        return string.Concat(PREFIX, collectionName, SEPARATOR, ELEMENT_TAIL, SEPARATOR, elementId);
    }

    public static bool TryGetCollectionName(string key, out string collectionName)
    {
        if (TryParse(key, out collectionName, out _, out _))
        {
            return true;
        }

        collectionName = string.Empty;
        return false;
    }

    public static bool TryParseElement(string key, string collectionName, out string elementId)
    {
        if (TryParse(key, out string parsedName, out bool isElement, out elementId)
            && isElement
            && parsedName.Equals(collectionName, StringComparison.Ordinal))
        {
            return true;
        }

        elementId = string.Empty;
        return false;
    }

    public static bool IsMarker(string key) =>
        TryParse(key, out _, out bool isElement, out _) && !isElement;

    public static bool IsMarker(string key, string collectionName) =>
        TryParse(key, out string parsedName, out bool isElement, out _)
        && !isElement
        && parsedName.Equals(collectionName, StringComparison.Ordinal);

    private static bool TryParse(string key, out string collectionName, out bool isElement, out string elementId)
    {
        collectionName = string.Empty;
        isElement = false;
        elementId = string.Empty;
        if (!key.StartsWith(PREFIX, StringComparison.Ordinal))
        {
            return false;
        }

        string rest = key[PREFIX.Length..];
        int separator = rest.IndexOf(SEPARATOR);
        if (separator <= 0 || separator == rest.Length - 1)
        {
            return false;
        }

        collectionName = rest[..separator];
        string tail = rest[(separator + 1)..];
        if (tail.Equals(MARKER_TAIL, StringComparison.Ordinal))
        {
            return true;
        }

        string elementPrefix = ELEMENT_TAIL + SEPARATOR;
        if (!tail.StartsWith(elementPrefix, StringComparison.Ordinal) || tail.Length == elementPrefix.Length)
        {
            collectionName = string.Empty;
            return false;
        }

        isElement = true;
        elementId = tail[elementPrefix.Length..];
        return true;
    }
}
