using System.Collections;

namespace Cyborg.TestModules.Validation;

public struct ValidationPipelineStructCollection<T> : ICollection<T>
{
    private List<T>? _items;

    public ValidationPipelineStructCollection()
    {
        _items = null;
    }

    public int Count => _items?.Count ?? 0;

    public bool IsReadOnly => false;

    public void Add(T item) => (_items ??= []).Add(item);

    public void Clear() => _items?.Clear();

    public bool Contains(T item) => _items?.Contains(item) ?? false;

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (_items is not null)
        {
            _items.CopyTo(array, arrayIndex);
        }
    }

    public bool Remove(T item) => _items?.Remove(item) ?? false;

    public IEnumerator<T> GetEnumerator() => (_items ?? []).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
