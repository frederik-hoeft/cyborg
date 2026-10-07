namespace Cyborg.Core.Runtime.Services.Transactions;

/// <summary>
/// Represents one stable fork point for a transaction-aware service state component.
/// </summary>
/// <typeparam name="TState">The participant-owned transaction state type.</typeparam>
public abstract class TransactionalServiceFork<TState> where TState : class
{
    /// <summary>
    /// Creates an isolated branch derived from this fork point.
    /// </summary>
    /// <remarks>
    /// Mutable state must not be shared between branches. Every branch must observe the same stable fork baseline
    /// without observing writes made by a sibling branch.
    /// </remarks>
    public abstract TState CreateBranch();

    /// <summary>
    /// Prepares a detached candidate state from the completed contributor branches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contributor index <c>0</c> is the owner continuation branch. Remaining contributors are child branches in
    /// fork creation order. These indices are the values reported to <paramref name="conflictResolver"/>.
    /// </para>
    /// <para>
    /// Preparation must not mutate state visible through the owner transaction. When conflicting contributors
    /// modify the same logical state, implementations must delegate the conflict to <paramref name="conflictResolver"/>.
    /// Return <see langword="false"/> only when the resolver rejects a reported conflict. Other preparation failures
    /// should be represented by an exception. A successful candidate must be safe to publish independently of the
    /// completed contributor states; mutable contributor state must not become the published owner state by aliasing.
    /// </para>
    /// </remarks>
    public abstract bool TryPrepareMerge(
        IReadOnlyList<TState> contributors,
        ITransactionalServiceConflictResolver conflictResolver,
        [NotNullWhen(true)] out TState? candidate);
}
