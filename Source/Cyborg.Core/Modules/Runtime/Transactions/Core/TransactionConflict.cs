using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal sealed record TransactionConflict(
    ITransactionParticipant Participant,
    object LogicalKey,
    ImmutableArray<int> ContributorIndices);
