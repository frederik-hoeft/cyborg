using System.Collections;

namespace Cyborg.Core.Runtime.Engine.Environments.VirtualCollections;

/// <summary>
/// Live view of one virtual collection.
/// Each visibility snapshot is consumed once before the view refreshes. The enumerator stops when a refresh exposes no new elements and never waits for later appends.
/// </summary>
internal sealed class VirtualCollectionLiveView(IEnvironmentVariableStore store, string collectionName) : IReadOnlyCollection<object?>
{
    public int Count => VirtualCollectionElements.Count(store, collectionName);

    public IEnumerator<object?> GetEnumerator()
    {
        HashSet<string> yieldedIds = new(StringComparer.Ordinal);
        while (VirtualCollectionElements.TryReadEntries(store, collectionName, out List<(string Id, object? Value)> entries))
        {
            bool yieldedAny = false;
            foreach ((string elementId, object? value) in entries)
            {
                if (!yieldedIds.Add(elementId))
                {
                    continue;
                }

                yieldedAny = true;
                yield return value;
            }

            if (!yieldedAny)
            {
                yield break;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
