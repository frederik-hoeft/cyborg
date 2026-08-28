using System.Text.RegularExpressions;

namespace Cyborg.Core.Runtime.Services.Debugging.Breakpoints;

/// <summary>
/// A single breakpoint expression registered for the current workflow execution.
/// Expressions are interpreted as regular expressions matched against module id, name, and group.
/// </summary>
public sealed record BreakpointExpression(int Id, string Expression, bool IsOneShot = false)
{
    private static readonly TimeSpan s_defaultMatchTimeout = TimeSpan.FromSeconds(1);

    internal Regex Regex { get; private init; } = CreateRegex(Expression, s_defaultMatchTimeout);

    internal BreakpointExpression(int id, string expression, bool isOneShot, TimeSpan matchTimeout)
        : this(id, expression, isOneShot)
    {
        Regex = CreateRegex(expression, matchTimeout);
    }

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

    private static Regex CreateRegex(string expression, TimeSpan matchTimeout) =>
        new(expression, RegexOptions.CultureInvariant | RegexOptions.Compiled, matchTimeout);
}
