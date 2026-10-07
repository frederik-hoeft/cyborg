using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Transactions;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugBranchControlFork(DebugBranchControlState ownerState) : TransactionalServiceFork<DebugBranchControlState>
{
    private readonly long _sessionGeneration = ownerState?.SessionGeneration ?? throw new ArgumentNullException(nameof(ownerState));
    private readonly long _controlCommandSequence = ownerState.ControlCommandSequence;
    private readonly bool _requiresCommandOrdering = ownerState.RequiresCommandOrdering;
    private readonly bool _isStepping = ownerState.IsStepping;
    private readonly ModuleExecutionId? _stepOverAnchor = ownerState.StepOverAnchor;

    public override DebugBranchControlState CreateBranch() =>
        new(_sessionGeneration, _isStepping, _stepOverAnchor, _controlCommandSequence, _requiresCommandOrdering);

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

        long newestSessionGeneration = contributors[0].SessionGeneration;
        for (int i = 1; i < contributors.Count; i++)
        {
            newestSessionGeneration = Math.Max(newestSessionGeneration, contributors[i].SessionGeneration);
        }

        bool requiresCommandOrdering = false;
        for (int i = 0; i < contributors.Count; i++)
        {
            DebugBranchControlState contributor = contributors[i];
            if (contributor.SessionGeneration == newestSessionGeneration && contributor.RequiresCommandOrdering)
            {
                requiresCommandOrdering = true;
                break;
            }
        }

        // Contributor 0 starts as the pre-fork owner continuation. When children exist, an untouched
        // continuation must not resurrect control state after every child explicitly replaced it. Once
        // step-over ordering is active, issuing even the same visible command is significant because its
        // command sequence makes it newer than sibling decisions.
        bool continuationChanged = contributors.Count > 1
            && (contributors[0].SessionGeneration != _sessionGeneration
                || contributors[0].IsStepping != _isStepping
                || contributors[0].StepOverAnchor != _stepOverAnchor
                || contributors[0].RequiresCommandOrdering != _requiresCommandOrdering
                || (requiresCommandOrdering && contributors[0].ControlCommandSequence != _controlCommandSequence));
        int firstContributor = contributors.Count > 1 && !continuationChanged ? 1 : 0;

        if (requiresCommandOrdering)
        {
            candidate = MergeOrderedCommands(contributors, firstContributor, newestSessionGeneration);
            return true;
        }

        candidate = MergeLegacyStepping(contributors, firstContributor, newestSessionGeneration);
        return true;
    }

    private static DebugBranchControlState MergeOrderedCommands(
        IReadOnlyList<DebugBranchControlState> contributors,
        int firstContributor,
        long sessionGeneration)
    {
        DebugBranchControlState? selected = null;
        long latestCommandSequence = long.MinValue;
        for (int i = firstContributor; i < contributors.Count; i++)
        {
            DebugBranchControlState contributor = contributors[i];
            if (contributor.SessionGeneration != sessionGeneration || contributor.ControlCommandSequence <= latestCommandSequence)
            {
                continue;
            }

            selected = contributor;
            latestCommandSequence = contributor.ControlCommandSequence;
        }

        if (selected is null)
        {
            throw new InvalidOperationException("Debugger branch-control reconciliation found no contributor in the newest session generation.");
        }

        return new DebugBranchControlState(
            sessionGeneration,
            selected.IsStepping,
            selected.StepOverAnchor,
            latestCommandSequence,
            requiresCommandOrdering: true);
    }

    private static DebugBranchControlState MergeLegacyStepping(
        IReadOnlyList<DebugBranchControlState> contributors,
        int firstContributor,
        long sessionGeneration)
    {
        bool isStepping = false;
        long latestCommandSequence = 0;
        for (int i = firstContributor; i < contributors.Count; i++)
        {
            DebugBranchControlState contributor = contributors[i];
            if (contributor.SessionGeneration != sessionGeneration)
            {
                continue;
            }

            isStepping |= contributor.IsStepping;
            latestCommandSequence = Math.Max(latestCommandSequence, contributor.ControlCommandSequence);
        }

        return new DebugBranchControlState(
            sessionGeneration,
            isStepping,
            stepOverAnchor: null,
            controlCommandSequence: latestCommandSequence,
            requiresCommandOrdering: false);
    }
}
