using Cyborg.Core.Runtime.Services.Transactions;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugBranchControlFork(DebugBranchControlState ownerState) : TransactionalServiceFork<DebugBranchControlState>
{
    private readonly long _sessionGeneration = ownerState?.SessionGeneration ?? throw new ArgumentNullException(nameof(ownerState));
    private readonly bool _isStepping = ownerState.IsStepping;

    public override DebugBranchControlState CreateBranch() => new(_sessionGeneration, _isStepping);

    public override bool TryPrepareMerge(
        IReadOnlyList<DebugBranchControlState> contributors,
        ITransactionalServiceConflictResolver conflictResolver,
        [NotNullWhen(true)] out DebugBranchControlState? candidate)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(conflictResolver);
        if (contributors.Count == 0)
        {
            throw new InvalidOperationException("Debugger branch-control reconciliation requires at least the owner continuation contributor.");
        }

        // Contributor 0 is the frozen owner continuation. When children exist it carries the pre-fork
        // step state, not a debugger decision made after the fork. Including it would resurrect stale
        // stepping after every child explicitly continued.
        int firstContributor = contributors.Count > 1 ? 1 : 0;
        long newestGeneration = contributors[firstContributor].SessionGeneration;
        for (int i = firstContributor + 1; i < contributors.Count; i++)
        {
            newestGeneration = Math.Max(newestGeneration, contributors[i].SessionGeneration);
        }

        // Session invalidation is global and may occur while a fork is open. Only contributors from
        // the newest represented generation may restore step state; older generations are stale.
        bool isStepping = false;
        for (int i = firstContributor; i < contributors.Count; i++)
        {
            DebugBranchControlState contributor = contributors[i];
            if (contributor.SessionGeneration == newestGeneration && contributor.IsStepping)
            {
                isStepping = true;
                break;
            }
        }

        candidate = new DebugBranchControlState(newestGeneration, isStepping);
        return true;
    }
}
