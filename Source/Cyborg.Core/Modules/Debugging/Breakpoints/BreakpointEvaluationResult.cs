using Cyborg.Core.Modules.Debugging;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Debugging.Breakpoints;

public sealed class BreakpointEvaluationResult
{
    private BreakpointEvaluationResult(BreakpointEvaluationStatus status, BreakpointExpression? breakpoint, ImmutableArray<DebugDiagnostic> diagnostics)
    {
        Status = status;
        Breakpoint = breakpoint;
        Diagnostics = diagnostics;
    }

    public BreakpointEvaluationStatus Status { get; }

    public BreakpointExpression? Breakpoint { get; }

    public ImmutableArray<DebugDiagnostic> Diagnostics { get; }

    public bool ShouldPause => Status is BreakpointEvaluationStatus.Match or BreakpointEvaluationStatus.Faulted;

    internal static BreakpointEvaluationResult NoMatch { get; } = new(BreakpointEvaluationStatus.NoMatch, breakpoint: null, diagnostics: []);

    internal static BreakpointEvaluationResult Match(BreakpointExpression breakpoint) => new(BreakpointEvaluationStatus.Match, breakpoint, diagnostics: []);

    internal static BreakpointEvaluationResult Faulted(BreakpointExpression breakpoint, DebugDiagnostic diagnostic) =>
        new(BreakpointEvaluationStatus.Faulted, breakpoint, [diagnostic]);
}
