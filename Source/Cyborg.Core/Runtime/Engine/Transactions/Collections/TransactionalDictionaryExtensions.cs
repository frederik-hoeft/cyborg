namespace Cyborg.Core.Runtime.Engine.Transactions.Collections;

internal static class TransactionalDictionaryExtensions
{
    extension<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> values) where TKey : notnull
    {
        public TransactionalDictionary<TKey, TValue> ToTransactionalDictionary(IEqualityComparer<TKey>? keyComparer = null) =>
            new(values, keyComparer);
    }
}
