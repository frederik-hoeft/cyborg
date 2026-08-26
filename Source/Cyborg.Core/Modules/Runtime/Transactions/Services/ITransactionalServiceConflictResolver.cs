namespace Cyborg.Core.Modules.Runtime.Transactions.Services;

/// <summary>
/// Resolves a logical write conflict detected while preparing transactional service state.
/// </summary>
public interface ITransactionalServiceConflictResolver
{
    /// <summary>
    /// Attempts to select one contributor for the supplied logical conflict.
    /// </summary>
    /// <param name="logicalKey">A participant-defined key identifying the conflicting logical state.</param>
    /// <param name="contributorIndices">Indices into the contributor list supplied to the current merge.</param>
    /// <param name="selectedContributorIndex">The selected contributor index when resolution succeeds.</param>
    /// <returns>
    /// <see langword="true"/> when the conflict strategy selected a contributor; otherwise <see langword="false"/>.
    /// A rejected conflict must cause the participant's current merge preparation to fail.
    /// </returns>
    bool TryResolve(
        object logicalKey,
        IReadOnlyList<int> contributorIndices,
        out int selectedContributorIndex);
}
