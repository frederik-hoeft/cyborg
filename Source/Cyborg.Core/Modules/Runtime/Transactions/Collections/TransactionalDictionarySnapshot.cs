using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions.Collections;

internal sealed class TransactionalDictionarySnapshot<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly ImmutableDictionary<TKey, TValue> _values;

    internal TransactionalDictionarySnapshot(ImmutableDictionary<TKey, TValue> values)
    {
        _values = values;
    }

    internal ImmutableDictionary<TKey, TValue> Data => _values;

    public int Count => _values.Count;

    public IEnumerable<TKey> Keys => _values.Keys;

    public IEnumerable<TValue> Values => _values.Values;

    public TValue this[TKey key] => _values[key];

    public bool ContainsKey(TKey key) => _values.ContainsKey(key);

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _values.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
