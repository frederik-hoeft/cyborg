using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Transactions.Internal;

internal sealed record TransactionConflict(ITransactionParticipant Participant, object LogicalKey, ImmutableArray<int> ContributorIndices);
