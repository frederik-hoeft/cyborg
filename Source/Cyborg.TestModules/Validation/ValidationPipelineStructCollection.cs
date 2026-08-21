using System.Collections;

namespace Cyborg.TestModules.Validation;

public struct ValidationPipelineStructCollection<T> : ICollection<T>
{
    private List<T>? _items;

    public ValidationPipelineStructCollection()
    {
        _items = null;
    }

    public readonly int Count => _items?.Count ?? 0;

    public readonly bool IsReadOnly => false;

    public void Add(T item) => (_items ??= []).Add(item);

    public readonly void Clear() => _items?.Clear();

    public readonly bool Contains(T item) => _items?.Contains(item) ?? false;

    public readonly void CopyTo(T[] array, int arrayIndex) => _items?.CopyTo(array, arrayIndex);

    public readonly bool Remove(T item) => _items?.Remove(item) ?? false;

    public readonly IEnumerator<T> GetEnumerator() => (_items ?? []).GetEnumerator();

    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
