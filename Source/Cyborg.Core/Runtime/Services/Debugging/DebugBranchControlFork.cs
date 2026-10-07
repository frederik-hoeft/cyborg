using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Transactions;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugBranchControlFork(DebugBranchControlState ownerState) : TransactionalServiceFork<DebugBranchControlState>
{
    private readonly long _sessionGeneration = ownerState?.SessionGeneration ?? throw new ArgumentNullException(nameof(ownerState));
    private readonly bool _isStepping = ownerState.IsStepping;
    private readonly ModuleExecutionId? _stepOverAnchor = ownerState.StepOverAnchor;

    public override DebugBranchControlState CreateBranch() => new(_sessionGeneration, _isStepping, _stepOverAnchor);

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

        // Contributor 0 starts as the pre-fork owner continuation. When children exist, that untouched
        // copy must not resurrect stepping or a step-over anchor after every child explicitly continued.
        // A continuation whose generation, step flag, or anchor differs from the fork baseline is a
        // decision made while the fork was open, and it participates like any other contributor.
        bool continuationChanged = contributors.Count > 1
            && (contributors[0].SessionGeneration != _sessionGeneration
                || contributors[0].IsStepping != _isStepping
                || contributors[0].StepOverAnchor != _stepOverAnchor);
        int firstContributor = contributors.Count > 1 && !continuationChanged ? 1 : 0;
        long newestGeneration = contributors[firstContributor].SessionGeneration;
        for (int i = firstContributor + 1; i < contributors.Count; i++)
        {
            newestGeneration = Math.Max(newestGeneration, contributors[i].SessionGeneration);
        }

        // Session invalidation is global and may occur while a fork is open. Only contributors from
        // the newest represented generation may restore step or step-over state; older generations are stale.
        // Step-into outranks a pending step-over: a branch that is still stepping pauses at every following
        // boundary, which already includes the module a step-over would have stopped on. Otherwise one
        // newest-generation anchor is preserved so an unsatisfied step-over continues on the parent.
        bool isStepping = false;
        ModuleExecutionId? stepOverAnchor = null;
        for (int i = firstContributor; i < contributors.Count; i++)
        {
            DebugBranchControlState contributor = contributors[i];
            if (contributor.SessionGeneration != newestGeneration)
            {
                continue;
            }

            if (contributor.IsStepping)
            {
                isStepping = true;
            }
            else if (stepOverAnchor is null && contributor.StepOverAnchor is { } anchor)
            {
                stepOverAnchor = anchor;
            }
        }

        candidate = new DebugBranchControlState(newestGeneration, isStepping, isStepping ? null : stepOverAnchor);
        return true;
    }
}
