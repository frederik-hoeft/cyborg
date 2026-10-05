using System.Collections;

namespace Cyborg.Core.Runtime.Engine.Environments.VirtualCollections;

internal static class VirtualCollectionElements
{
    private static readonly object DefinitionMarker = new();

    public static void Append(IEnvironmentVariableStore store, string collectionName, object? element)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(collectionName);
        string orderToken = store.AllocateCollectionOrderToken();
        store.SetValue(VirtualCollectionKeys.Element(collectionName, orderToken), element);
    }

    public static void Define(IEnvironmentVariableStore store, string collectionName, object? value)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(collectionName);
        object?[] elements = Materialize(value);
        RemoveKeys(store, collectionName);
        store.SetValue(VirtualCollectionKeys.Marker(collectionName), DefinitionMarker);
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
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(collectionName);
        bool defined = false;
        List<(string Token, object? Value)> found = [];
        foreach ((string key, object? value) in store)
        {
            if (VirtualCollectionKeys.IsMarker(key, collectionName))
            {
                defined = true;
                continue;
            }

            if (VirtualCollectionKeys.TryParseElement(key, collectionName, out string orderToken))
            {
                found.Add((orderToken, value));
            }
        }

        if (!defined && found.Count == 0)
        {
            elements = [];
            return false;
        }

        found.Sort(static (left, right) => string.CompareOrdinal(left.Token, right.Token));
        elements = new object?[found.Count];
        for (int index = 0; index < found.Count; index++)
        {
            elements[index] = found[index].Value;
        }

        return true;
    }

    public static bool TryGetElement(IEnvironmentVariableStore store, string collectionName, int index, out object? element)
    {
        if (index < 0 || !TryRead(store, collectionName, out object?[] elements) || index >= elements.Length)
        {
            element = null;
            return false;
        }

        element = elements[index];
        return true;
    }

    public static int Count(IEnvironmentVariableStore store, string collectionName) =>
        TryRead(store, collectionName, out object?[] elements) ? elements.Length : 0;

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
