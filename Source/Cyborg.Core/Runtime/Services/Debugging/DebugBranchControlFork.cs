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
            throw new InvalidOperationException("Debugger branch-control reconciliation requires at least one contributor.");
        }

        long newestSessionGeneration = contributors[0].SessionGeneration;
        for (int i = 1; i < contributors.Count; i++)
        {
            newestSessionGeneration = Math.Max(newestSessionGeneration, contributors[i].SessionGeneration);
        }

        DebugBranchControlState? selected = null;
        long latestCommandSequence = long.MinValue;
        for (int i = 0; i < contributors.Count; i++)
        {
            DebugBranchControlState contributor = contributors[i];
            if (contributor.SessionGeneration != newestSessionGeneration)
            {
                continue;
            }
            if (contributor.ControlCommandSequence < latestCommandSequence)
            {
                continue;
            }
            if (contributor.ControlCommandSequence == latestCommandSequence)
            {
                if (selected is not null && !HasEquivalentControlState(selected, contributor))
                {
                    throw new InvalidOperationException("Debugger branch-control contributors with the same command sequence have divergent control state.");
                }
                continue;
            }

            selected = contributor;
            latestCommandSequence = contributor.ControlCommandSequence;
        }

        if (selected is null)
        {
            throw new InvalidOperationException("Debugger branch-control reconciliation found no contributor in the newest session generation.");
        }

        candidate = new DebugBranchControlState(
            newestSessionGeneration,
            selected.IsStepping,
            selected.StepOverAnchor,
            selected.ControlCommandSequence);
        return true;
    }

    private static bool HasEquivalentControlState(DebugBranchControlState first, DebugBranchControlState second) =>
        first.IsStepping == second.IsStepping && first.StepOverAnchor == second.StepOverAnchor;
}
