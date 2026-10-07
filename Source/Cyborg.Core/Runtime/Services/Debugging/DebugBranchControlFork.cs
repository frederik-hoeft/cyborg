using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Transactions;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebugBranchControlFork(DebugBranchControlState ownerState) : TransactionalServiceFork<DebugBranchControlState>
{
    private readonly long _sessionGeneration = ownerState?.SessionGeneration ?? throw new ArgumentNullException(nameof(ownerState));
    private readonly long _controlCommandSequence = ownerState.ControlCommandSequence;
    private readonly bool _isStepping = ownerState.IsStepping;
    private readonly ModuleExecutionId? _stepOverAnchor = ownerState.StepOverAnchor;

    public override DebugBranchControlState CreateBranch() =>
        new(_sessionGeneration, _isStepping, _stepOverAnchor, _controlCommandSequence);

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

        // Compatibility path for the flattened public API. Runtime reconciliation calls the role-aware overload below.
        DebugBranchControlState[] children = new DebugBranchControlState[contributors.Count - 1];
        for (int i = 1; i < contributors.Count; i++)
        {
            children[i - 1] = contributors[i];
        }
        return TryPrepareMerge(contributors[0], children, conflictResolver, out candidate);
    }

    public override bool TryPrepareMerge(
        DebugBranchControlState ownerContinuation,
        IReadOnlyList<DebugBranchControlState> children,
        ITransactionalServiceConflictResolver conflictResolver,
        [NotNullWhen(true)] out DebugBranchControlState? candidate)
    {
        ArgumentNullException.ThrowIfNull(ownerContinuation);
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(conflictResolver);

        long newestSessionGeneration = ownerContinuation.SessionGeneration;
        for (int i = 0; i < children.Count; i++)
        {
            newestSessionGeneration = Math.Max(newestSessionGeneration, children[i].SessionGeneration);
        }

        if (RequiresCommandOrdering(ownerContinuation, children, newestSessionGeneration))
        {
            candidate = MergeOrderedCommands(ownerContinuation, children, newestSessionGeneration);
            return true;
        }

        candidate = MergeStepping(ownerContinuation, children, newestSessionGeneration);
        return true;
    }

    private bool RequiresCommandOrdering(
        DebugBranchControlState ownerContinuation,
        IReadOnlyList<DebugBranchControlState> children,
        long sessionGeneration)
    {
        if (_sessionGeneration == sessionGeneration && _stepOverAnchor is not null)
        {
            return true;
        }
        if (ownerContinuation.SessionGeneration == sessionGeneration && ownerContinuation.StepOverAnchor is not null)
        {
            return true;
        }
        for (int i = 0; i < children.Count; i++)
        {
            DebugBranchControlState child = children[i];
            if (child.SessionGeneration == sessionGeneration && child.StepOverAnchor is not null)
            {
                return true;
            }
        }
        return false;
    }

    private DebugBranchControlState MergeOrderedCommands(
        DebugBranchControlState ownerContinuation,
        IReadOnlyList<DebugBranchControlState> children,
        long sessionGeneration)
    {
        DebugBranchControlState? selected = null;
        long latestCommandSequence = long.MinValue;
        if (ShouldIncludeOwnerContinuation(ownerContinuation, children.Count)
            && ownerContinuation.SessionGeneration == sessionGeneration)
        {
            selected = ownerContinuation;
            latestCommandSequence = ownerContinuation.ControlCommandSequence;
        }

        for (int i = 0; i < children.Count; i++)
        {
            DebugBranchControlState child = children[i];
            if (child.SessionGeneration != sessionGeneration || child.ControlCommandSequence <= latestCommandSequence)
            {
                continue;
            }

            selected = child;
            latestCommandSequence = child.ControlCommandSequence;
        }

        if (selected is null)
        {
            throw new InvalidOperationException("Debugger branch-control reconciliation found no contributor in the newest session generation.");
        }

        return new DebugBranchControlState(sessionGeneration, selected.IsStepping, selected.StepOverAnchor, latestCommandSequence);
    }

    private DebugBranchControlState MergeStepping(
        DebugBranchControlState ownerContinuation,
        IReadOnlyList<DebugBranchControlState> children,
        long sessionGeneration)
    {
        bool hasContributor = false;
        bool isStepping = false;
        long latestCommandSequence = long.MinValue;
        if (ShouldIncludeOwnerContinuation(ownerContinuation, children.Count)
            && ownerContinuation.SessionGeneration == sessionGeneration)
        {
            hasContributor = true;
            isStepping = ownerContinuation.IsStepping;
            latestCommandSequence = ownerContinuation.ControlCommandSequence;
        }

        for (int i = 0; i < children.Count; i++)
        {
            DebugBranchControlState child = children[i];
            if (child.SessionGeneration != sessionGeneration)
            {
                continue;
            }

            hasContributor = true;
            isStepping |= child.IsStepping;
            latestCommandSequence = Math.Max(latestCommandSequence, child.ControlCommandSequence);
        }

        if (!hasContributor)
        {
            throw new InvalidOperationException("Debugger branch-control reconciliation found no contributor in the newest session generation.");
        }

        return new DebugBranchControlState(sessionGeneration, isStepping, stepOverAnchor: null, latestCommandSequence);
    }

    private bool ShouldIncludeOwnerContinuation(DebugBranchControlState ownerContinuation, int childCount) =>
        childCount == 0
        || ownerContinuation.SessionGeneration != _sessionGeneration
        || ownerContinuation.ControlCommandSequence != _controlCommandSequence;
}
