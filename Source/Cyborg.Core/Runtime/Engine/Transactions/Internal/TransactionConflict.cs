using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal sealed record TransactionConflict(ITransactionParticipant Participant, object LogicalKey, ImmutableArray<int> ContributorIndices)
{
    public InvalidOperationException ToException() =>
        new($"Module transaction reconciliation failed due to a conflict in participant '{Participant.GetType().Name}' for logical key '{LogicalKey}'.");
}
