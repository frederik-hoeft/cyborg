using System.Collections;

namespace Cyborg.Core.Runtime.Engine.Environments.VirtualCollections;

/// <summary>
/// Live view of one virtual collection.
/// MoveNext observes elements that are already visible and stops at the current end. It does not wait for later appends.
/// </summary>
internal sealed class VirtualCollectionLiveView(IEnvironmentVariableStore store, string collectionName) : IReadOnlyCollection<object?>
{
    public int Count => VirtualCollectionElements.Count(store, collectionName);

    public IEnumerator<object?> GetEnumerator()
    {
        for (int index = 0; ; index++)
        {
            if (!VirtualCollectionElements.TryGetElement(store, collectionName, index, out object? element))
            {
                yield break;
            }

            yield return element;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
