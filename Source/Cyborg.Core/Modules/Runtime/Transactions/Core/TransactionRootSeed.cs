using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal sealed class TransactionRootSeed
{
    private readonly ImmutableDictionary<ITransactionParticipant, object?> _values;

    public TransactionRootSeed()
        : this(ImmutableDictionary.Create<ITransactionParticipant, object?>(ReferenceEqualityComparer.Instance))
    {
    }

    private TransactionRootSeed(ImmutableDictionary<ITransactionParticipant, object?> values)
    {
        _values = values;
    }

    public TransactionRootSeed With<TSeed>(ITransactionParticipant participant, TSeed seed)
        where TSeed : notnull
    {
        ArgumentNullException.ThrowIfNull(participant);
        return new TransactionRootSeed(_values.SetItem(participant, seed));
    }

    public bool TryGet<TSeed>(ITransactionParticipant participant, out TSeed seed)
        where TSeed : notnull
    {
        ArgumentNullException.ThrowIfNull(participant);
        if (_values.TryGetValue(participant, out object? value) && value is TSeed typedSeed)
        {
            seed = typedSeed;
            return true;
        }
        seed = default!;
        return false;
    }
}
