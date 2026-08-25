using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Transactions.Collections;

/// <summary>
/// Represents one transaction-local dictionary view as an immutable baseline plus explicit local changes.
/// </summary>
/// <remarks>
/// Instances are single-writer. Parallel branches must use distinct instances created from the same frozen baseline.
/// </remarks>
internal sealed class TransactionalDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly TransactionalDictionarySnapshot<TKey, TValue> _baseline;
    private readonly Dictionary<TKey, TransactionalDictionaryChange<TValue>> _changes;
    private readonly Dictionary<TKey, TransactionalDictionaryChange<TValue>> _pendingSnapshotChanges;
    private TransactionalDictionarySnapshot<TKey, TValue> _effectiveSnapshot;

    public TransactionalDictionary(IEqualityComparer<TKey>? keyComparer = null)
        : this(CreateEmptySnapshot(keyComparer))
    {
    }

    public TransactionalDictionary(IEnumerable<KeyValuePair<TKey, TValue>> values, IEqualityComparer<TKey>? keyComparer = null)
        : this(CreateSnapshot(values, keyComparer))
    {
    }

    private TransactionalDictionary(TransactionalDictionarySnapshot<TKey, TValue> baseline)
        : this(
            baseline,
            new Dictionary<TKey, TransactionalDictionaryChange<TValue>>(baseline.Data.KeyComparer),
            baseline)
    {
    }

    private TransactionalDictionary(
        TransactionalDictionarySnapshot<TKey, TValue> baseline,
        Dictionary<TKey, TransactionalDictionaryChange<TValue>> changes,
        TransactionalDictionarySnapshot<TKey, TValue> effectiveSnapshot)
    {
        _baseline = baseline;
        _changes = changes;
        _pendingSnapshotChanges = new Dictionary<TKey, TransactionalDictionaryChange<TValue>>(baseline.Data.KeyComparer);
        _effectiveSnapshot = effectiveSnapshot;
    }

    internal TransactionalDictionarySnapshot<TKey, TValue> Baseline => _baseline;

    internal int ChangeCount => _changes.Count;

    public int Count => Freeze().Count;

    public IEnumerable<TKey> Keys => Freeze().Keys;

    public IEnumerable<TValue> Values => Freeze().Values;

    public TValue this[TKey key]
    {
        get
        {
            if (TryGetValue(key, out TValue? value))
            {
                return value;
            }
            throw new KeyNotFoundException();
        }
    }

    public bool ContainsKey(TKey key) => TryGetValue(key, out TValue? _);

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_changes.TryGetValue(key, out TransactionalDictionaryChange<TValue> change))
        {
            if (change.Kind == TransactionalDictionaryChangeKind.Remove)
            {
                value = default;
                return false;
            }
            value = change.Value;
            return true;
        }
        return _baseline.TryGetValue(key, out value);
    }

    public void Set(TKey key, TValue value)
    {
        TransactionalDictionaryChange<TValue> change = TransactionalDictionaryChange<TValue>.Set(value);
        _changes[key] = change;
        _pendingSnapshotChanges[key] = change;
    }

    public bool TryAdd(TKey key, TValue value)
    {
        if (ContainsKey(key))
        {
            return false;
        }
        Set(key, value);
        return true;
    }

    public bool TryRemove(TKey key)
    {
        if (!ContainsKey(key))
        {
            return false;
        }
        TransactionalDictionaryChange<TValue> change = TransactionalDictionaryChange<TValue>.Remove();
        _changes[key] = change;
        _pendingSnapshotChanges[key] = change;
        return true;
    }

    internal bool TryGetChange(TKey key, out TransactionalDictionaryChange<TValue> change) =>
        _changes.TryGetValue(key, out change);

    internal IEnumerable<KeyValuePair<TKey, TransactionalDictionaryChange<TValue>>> EnumerateChanges() => _changes;

    internal TransactionalDictionarySnapshot<TKey, TValue> Freeze()
    {
        if (_pendingSnapshotChanges.Count == 0)
        {
            return _effectiveSnapshot;
        }

        ImmutableDictionary<TKey, TValue>.Builder builder = _effectiveSnapshot.Data.ToBuilder();
        ApplyChanges(builder, _pendingSnapshotChanges);
        _effectiveSnapshot = new TransactionalDictionarySnapshot<TKey, TValue>(builder.ToImmutable());
        _pendingSnapshotChanges.Clear();
        return _effectiveSnapshot;
    }

    internal TransactionalDictionary<TKey, TValue> Fork() => new(Freeze());

    internal bool TryPrepareMerge(
        TransactionalDictionarySnapshot<TKey, TValue> forkBaseline,
        IReadOnlyCollection<TransactionalDictionary<TKey, TValue>> contributors,
        [NotNullWhen(true)] out TransactionalDictionary<TKey, TValue>? candidate,
        out TKey conflictKey)
    {
        ArgumentNullException.ThrowIfNull(forkBaseline);
        ArgumentNullException.ThrowIfNull(contributors);
        if (!ReferenceEquals(Freeze(), forkBaseline))
        {
            throw new InvalidOperationException("The dictionary changed after the fork baseline was captured.");
        }

        Dictionary<TKey, TransactionalDictionaryChange<TValue>> contributorChanges = new(_baseline.Data.KeyComparer);
        foreach (TransactionalDictionary<TKey, TValue> contributor in contributors)
        {
            ArgumentNullException.ThrowIfNull(contributor);
            if (!ReferenceEquals(contributor.Baseline, forkBaseline))
            {
                throw new ArgumentException("Every merge contributor must derive from the supplied fork baseline.", nameof(contributors));
            }
            foreach ((TKey key, TransactionalDictionaryChange<TValue> change) in contributor._changes)
            {
                if (!contributorChanges.TryAdd(key, change))
                {
                    candidate = null;
                    conflictKey = key;
                    return false;
                }
            }
        }

        candidate = PrepareMergeCandidate(forkBaseline, contributorChanges);
        conflictKey = default!;
        return true;
    }

    internal TransactionalDictionary<TKey, TValue> PrepareMergeCandidate(
        TransactionalDictionarySnapshot<TKey, TValue> forkBaseline,
        IReadOnlyDictionary<TKey, TransactionalDictionaryChange<TValue>> contributorChanges)
    {
        ArgumentNullException.ThrowIfNull(forkBaseline);
        ArgumentNullException.ThrowIfNull(contributorChanges);
        if (!ReferenceEquals(Freeze(), forkBaseline))
        {
            throw new InvalidOperationException("The dictionary changed after the fork baseline was captured.");
        }

        Dictionary<TKey, TransactionalDictionaryChange<TValue>> candidateChanges = new(_changes, _baseline.Data.KeyComparer);
        foreach ((TKey key, TransactionalDictionaryChange<TValue> change) in contributorChanges)
        {
            candidateChanges[key] = change;
        }

        ImmutableDictionary<TKey, TValue>.Builder builder = forkBaseline.Data.ToBuilder();
        ApplyChanges(builder, contributorChanges);
        TransactionalDictionarySnapshot<TKey, TValue> candidateSnapshot = new(builder.ToImmutable());
        return new TransactionalDictionary<TKey, TValue>(_baseline, candidateChanges, candidateSnapshot);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Freeze().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static void ApplyChanges(
        ImmutableDictionary<TKey, TValue>.Builder builder,
        IReadOnlyDictionary<TKey, TransactionalDictionaryChange<TValue>> changes)
    {
        foreach ((TKey key, TransactionalDictionaryChange<TValue> change) in changes)
        {
            switch (change.Kind)
            {
                case TransactionalDictionaryChangeKind.Set:
                    builder[key] = change.Value;
                    break;
                case TransactionalDictionaryChangeKind.Remove:
                    builder.Remove(key);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported transactional dictionary change kind '{change.Kind}'.");
            }
        }
    }

    private static TransactionalDictionarySnapshot<TKey, TValue> CreateEmptySnapshot(IEqualityComparer<TKey>? keyComparer)
    {
        ImmutableDictionary<TKey, TValue> values = ImmutableDictionary.Create<TKey, TValue>(keyComparer);
        return new TransactionalDictionarySnapshot<TKey, TValue>(values);
    }

    private static TransactionalDictionarySnapshot<TKey, TValue> CreateSnapshot(
        IEnumerable<KeyValuePair<TKey, TValue>> values,
        IEqualityComparer<TKey>? keyComparer)
    {
        ArgumentNullException.ThrowIfNull(values);
        ImmutableDictionary<TKey, TValue> snapshot = ImmutableDictionary.CreateRange(keyComparer, values);
        return new TransactionalDictionarySnapshot<TKey, TValue>(snapshot);
    }
}
