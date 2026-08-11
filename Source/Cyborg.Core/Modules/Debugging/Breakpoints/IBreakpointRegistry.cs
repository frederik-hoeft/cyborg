namespace Cyborg.Core.Modules.Debugging.Breakpoints;

/// <summary>
/// Thread-safe registry of breakpoint expressions for a workflow execution session.
/// </summary>
public interface IBreakpointRegistry
{
    int Count { get; }

    int Add(string expression, bool isOneShot = false);

    bool Remove(int id);

    void Clear();

    IReadOnlyList<BreakpointExpression> ToList();

    BreakpointEvaluationResult EvaluateAndConsume(ref readonly BreakpointContext context);

    BreakpointEvaluationResult EvaluateAndConsume(IEnumerable<string> targets);
}
