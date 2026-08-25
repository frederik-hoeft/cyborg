namespace Cyborg.Core.Modules.Runtime.Transactions.Core;

internal enum TransactionConflictResolutionKind
{
    Fail,
    UseContributor
}

internal readonly record struct TransactionConflictResolution
{
    private TransactionConflictResolution(TransactionConflictResolutionKind kind, int contributorIndex)
    {
        Kind = kind;
        ContributorIndex = contributorIndex;
    }

    public TransactionConflictResolutionKind Kind { get; }

    public int ContributorIndex { get; }

    public static TransactionConflictResolution Fail() =>
        new(TransactionConflictResolutionKind.Fail, contributorIndex: -1);

    public static TransactionConflictResolution UseContributor(int contributorIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(contributorIndex);
        return new TransactionConflictResolution(TransactionConflictResolutionKind.UseContributor, contributorIndex);
    }
}
