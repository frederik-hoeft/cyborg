using Cyborg.Core.Configuration.Model;

namespace Cyborg.Core.Configuration;

/// <summary>
/// Helpers for hierarchical path addressing and structured composition against
/// <see cref="IHierarchicalKeyValueStore"/>.
/// </summary>
public static class HierarchicalKeyValueStoreExtensions
{
    /// <summary>
    /// Combines a root path with a single hierarchical segment using the dotted path convention.
    /// </summary>
    public static string CombinePath(string rootPath, string segment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(segment);
        return $"{rootPath}.{segment}";
    }

    /// <summary>
    /// Returns whether the store contains the exact key or any descendant key under <paramref name="rootPath"/>.
    /// </summary>
    public static bool HasValues(this IHierarchicalKeyValueStore store, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        string prefix = rootPath + ".";
        foreach ((string key, object? _) in store)
        {
            if (key.Equals(rootPath, StringComparison.Ordinal) || key.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reconstructs a decomposable instance of <typeparamref name="T"/> from leaves under <paramref name="rootPath"/>.
    /// </summary>
    public static T Compose<T>(this IHierarchicalKeyValueStore store, string rootPath)
        where T : class, IDecomposable<T>
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        return T.Compose(store, rootPath);
    }

    /// <summary>
    /// Attempts to reconstruct a decomposable instance when the store has any values under <paramref name="rootPath"/>.
    /// </summary>
    public static bool TryCompose<T>(this IHierarchicalKeyValueStore store, string rootPath, [NotNullWhen(true)] out T? value)
        where T : class, IDecomposable<T>
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!store.HasValues(rootPath))
        {
            value = null;
            return false;
        }

        value = T.Compose(store, rootPath);
        return true;
    }
}
