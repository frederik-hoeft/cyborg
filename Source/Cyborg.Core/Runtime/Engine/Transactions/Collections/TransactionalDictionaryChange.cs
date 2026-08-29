namespace Cyborg.Core.Runtime.Engine.Transactions.Collections;

internal readonly record struct TransactionalDictionaryChange<TValue>
{
    private TransactionalDictionaryChange(TransactionalDictionaryChangeKind kind, TValue? value)
    {
        Kind = kind;
        Value = value;
    }

    public TransactionalDictionaryChangeKind Kind { get; }

    [AllowNull]
    public TValue Value => Kind == TransactionalDictionaryChangeKind.Set ? field! : throw new InvalidOperationException("A removal does not contain a value.");

    public static TransactionalDictionaryChange<TValue> Set(TValue value) =>
        new(TransactionalDictionaryChangeKind.Set, value);

    public static TransactionalDictionaryChange<TValue> Remove() =>
        new(TransactionalDictionaryChangeKind.Remove, default);
}
