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
    /// Prepares a detached candidate state from the completed owner continuation and child branches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="ownerContinuation"/> is the explicit continuation branch owned by the transaction that opened
    /// the fork. <paramref name="children"/> contains child branches in fork creation order.
    /// </para>
    /// <para>
    /// Conflict contributor index <c>0</c> identifies <paramref name="ownerContinuation"/>. Child index <c>i</c>
    /// maps to conflict contributor index <c>i + 1</c>.
    /// </para>
    /// <para>
    /// The default implementation adapts these explicit roles to the flattened contributor overload. Override this
    /// overload when reconciliation semantics depend on distinguishing the owner continuation from child branches.
    /// </para>
    /// </remarks>
    public virtual bool TryPrepareMerge(
        TState ownerContinuation,
        IReadOnlyList<TState> children,
        ITransactionalServiceConflictResolver conflictResolver,
        [NotNullWhen(true)] out TState? candidate)
    {
        ArgumentNullException.ThrowIfNull(ownerContinuation);
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(conflictResolver);

        TState[] contributors = new TState[children.Count + 1];
        contributors[0] = ownerContinuation;
        for (int i = 0; i < children.Count; i++)
        {
            contributors[i + 1] = children[i];
        }
        return TryPrepareMerge(contributors, conflictResolver, out candidate);
    }

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
