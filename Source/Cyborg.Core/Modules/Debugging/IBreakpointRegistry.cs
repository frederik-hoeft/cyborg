namespace Cyborg.Core.Modules.Debugging;

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

    /// <summary>
    /// Returns true if any registered expression matches the module identity fields.
    /// One-shot matches are removed as part of this call.
    /// </summary>
    bool TryMatchAndConsume(string moduleId, string? name, string? group, out BreakpointExpression? matched);
}
