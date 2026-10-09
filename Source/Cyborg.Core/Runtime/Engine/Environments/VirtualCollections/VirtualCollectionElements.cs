using System.Collections;

namespace Cyborg.Core.Runtime.Engine.Environments.VirtualCollections;

internal static class VirtualCollectionElements
{
    private static readonly object s_definitionMarker = new();
    private static readonly object s_emptyDefinitionMarker = new();

    public static void Append(IEnvironmentVariableStore store, string collectionName, object? element)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(collectionName);
        string elementId = VirtualCollectionElementId.Allocate();
        store.SetValue(VirtualCollectionKeys.Element(collectionName, elementId), element);
    }

    public static void Define(IEnvironmentVariableStore store, string collectionName, object? value)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(collectionName);
        object?[] elements = Materialize(value);
        RemoveKeys(store, collectionName);
        store.SetValue(VirtualCollectionKeys.Marker(collectionName), elements.Length == 0 ? s_emptyDefinitionMarker : s_definitionMarker);
        foreach (object? element in elements)
        {
            Append(store, collectionName, element);
        }
    }

    public static bool TryRemove(IEnvironmentVariableStore store, string collectionName)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(collectionName);
        return RemoveKeys(store, collectionName);
    }

    public static bool TryRead(IEnvironmentVariableStore store, string collectionName, out object?[] elements)
    {
        if (!TryReadEntries(store, collectionName, out List<(string Id, object? Value)> entries))
        {
            elements = [];
            return false;
        }

        elements = new object?[entries.Count];
        for (int index = 0; index < entries.Count; index++)
        {
            elements[index] = entries[index].Value;
        }
        return true;
    }

    internal static bool TryReadEntries(IEnvironmentVariableStore store, string collectionName, out List<(string Id, object? Value)> entries)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(collectionName);
        bool defined = false;
        entries = [];
        foreach ((string key, object? value) in store)
        {
            if (VirtualCollectionKeys.IsMarker(key, collectionName))
            {
                defined = true;
                continue;
            }

            if (VirtualCollectionKeys.TryParseElement(key, collectionName, out string elementId))
            {
                entries.Add((elementId, value));
            }
        }

        if (!defined && entries.Count == 0)
        {
            return false;
        }

        entries.Sort(static (left, right) => string.CompareOrdinal(left.Id, right.Id));
        return true;
    }

    public static int Count(IEnvironmentVariableStore store, string collectionName) =>
        TryRead(store, collectionName, out object?[] elements) ? elements.Length : 0;

    internal static bool IsMergeCompatibleDefinitionMarker(object? value) => ReferenceEquals(value, s_emptyDefinitionMarker);

    private static bool RemoveKeys(IEnvironmentVariableStore store, string collectionName)
    {
        List<string> keys = [];
        foreach ((string key, object? _) in store)
        {
            if (VirtualCollectionKeys.IsMarker(key, collectionName) || VirtualCollectionKeys.TryParseElement(key, collectionName, out _))
            {
                keys.Add(key);
            }
        }

        bool removed = false;
        foreach (string key in keys)
        {
            removed |= store.TryRemove(key);
        }

        return removed;
    }

    private static object?[] Materialize(object? value)
    {
        if (value is null)
        {
            return [];
        }

        if (value is string || value is not IEnumerable enumerable)
        {
            return [value];
        }

        List<object?> elements = [];
        foreach (object? element in enumerable)
        {
            elements.Add(element);
        }

        return [.. elements];
    }
}
