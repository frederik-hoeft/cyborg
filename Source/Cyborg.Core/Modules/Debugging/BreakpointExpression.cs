using System.Text.RegularExpressions;

namespace Cyborg.Core.Modules.Debugging;

/// <summary>
/// A single breakpoint expression registered for the current workflow execution.
/// Expressions are interpreted as regular expressions matched against module id, name, and group.
/// </summary>
// TODO: can probably be a record
public sealed class BreakpointExpression
{
    public BreakpointExpression(int id, string expression, bool isOneShot = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        Id = id;
        Expression = expression;
        IsOneShot = isOneShot;
        Regex = new Regex(expression, RegexOptions.CultureInvariant | RegexOptions.Compiled, matchTimeout: TimeSpan.FromSeconds(1));
    }

    public int Id { get; }

    public string Expression { get; }

    /// <summary>
    /// When true, the breakpoint is removed automatically after the next hit (used for step).
    /// </summary>
    public bool IsOneShot { get; }

    internal Regex Regex { get; }

    public bool Matches(string moduleId, string? name, string? group)
    {
        if (Regex.IsMatch(moduleId))
        {
            return true;
        }
        if (!string.IsNullOrEmpty(name) && Regex.IsMatch(name))
        {
            return true;
        }
        if (!string.IsNullOrEmpty(group) && Regex.IsMatch(group))
        {
            return true;
        }
        return false;
    }

    public override string ToString() => IsOneShot ? $"{Id}: {Expression} (one-shot)" : $"{Id}: {Expression}";
}
