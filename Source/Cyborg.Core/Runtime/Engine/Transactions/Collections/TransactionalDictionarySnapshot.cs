using System.Collections;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Transactions.Collections;

internal sealed record TransactionalDictionarySnapshot<TKey, TValue>(ImmutableDictionary<TKey, TValue> Data) : IReadOnlyDictionary<TKey, TValue> where TKey : notnull
{
    public int Count => Data.Count;

    public IEnumerable<TKey> Keys => Data.Keys;

    public IEnumerable<TValue> Values => Data.Values;

    public TValue this[TKey key] => Data[key];

    public bool ContainsKey(TKey key) => Data.ContainsKey(key);

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => Data.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Data.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
