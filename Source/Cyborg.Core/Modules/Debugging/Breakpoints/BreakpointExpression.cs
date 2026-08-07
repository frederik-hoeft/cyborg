using System.Text.RegularExpressions;

namespace Cyborg.Core.Modules.Debugging.Breakpoints;

/// <summary>
/// A single breakpoint expression registered for the current workflow execution.
/// Expressions are interpreted as regular expressions matched against module id, name, and group.
/// </summary>
public sealed record BreakpointExpression(int Id, string Expression, bool IsOneShot = false)
{
    internal Regex Regex { get; } = new Regex(Expression, RegexOptions.CultureInvariant | RegexOptions.Compiled, matchTimeout: TimeSpan.FromSeconds(1));

    public bool MatchesAny(IEnumerable<string> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        foreach (string target in targets)
        {
            if (Regex.IsMatch(target))
            {
                return true;
            }
        }
        return false;
    }

    public override string ToString() => IsOneShot ? $"{Id}: {Expression} (one-shot)" : $"{Id}: {Expression}";
}
