namespace Cyborg.Core.Modules.Runtime.Transactions.Collections;

internal enum TransactionalDictionaryChangeKind
{
    Set,
    Remove
}

internal readonly record struct TransactionalDictionaryChange<TValue>
{
    private readonly TValue? _value;

    private TransactionalDictionaryChange(TransactionalDictionaryChangeKind kind, TValue? value)
    {
        Kind = kind;
        _value = value;
    }

    public TransactionalDictionaryChangeKind Kind { get; }

    public TValue Value => Kind == TransactionalDictionaryChangeKind.Set
        ? _value!
        : throw new InvalidOperationException("A removal does not contain a value.");

    public static TransactionalDictionaryChange<TValue> Set(TValue value) =>
        new(TransactionalDictionaryChangeKind.Set, value);

    public static TransactionalDictionaryChange<TValue> Remove() =>
        new(TransactionalDictionaryChangeKind.Remove, default);
}
