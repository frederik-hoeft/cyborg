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

    IReadOnlyList<BreakpointExpression> List();

    bool TryMatchAndConsume(ref readonly BreakpointContext context, out BreakpointExpression? matched);

    bool TryMatchAndConsume(IEnumerable<string> targets, out BreakpointExpression? matched);
}
