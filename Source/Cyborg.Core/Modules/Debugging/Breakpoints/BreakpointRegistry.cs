using Cyborg.Core.Modules.Debugging;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Modules.Debugging.Breakpoints;

public sealed class BreakpointRegistry : IBreakpointRegistry
{
    private static readonly TimeSpan s_defaultMatchTimeout = TimeSpan.FromSeconds(1);

    private readonly ConcurrentDictionary<int, BreakpointExpression> _breakpoints = [];
    private readonly TimeSpan _matchTimeout;
    private int _lastId = 0;

    public BreakpointRegistry()
        : this(s_defaultMatchTimeout)
    {
    }

    internal BreakpointRegistry(TimeSpan matchTimeout)
    {
        if (matchTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(matchTimeout), matchTimeout, "Breakpoint match timeout must be positive.");
        }
        _matchTimeout = matchTimeout;
    }

    public int Count => _breakpoints.Count;

    public int Add(string expression, bool isOneShot = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        int id = Interlocked.Increment(ref _lastId);

        BreakpointExpression breakpoint = new(id, expression, isOneShot, _matchTimeout);
        if (!_breakpoints.TryAdd(id, breakpoint))
        {
            // this should never happen, but if you manage to overflow the counter, this will throw on duplicate keys
            throw new InvalidOperationException($"Failed to add breakpoint with id {id}.");
        }
        return id;
    }

    public bool Remove(int id) => _breakpoints.TryRemove(id, out _);

    public void Clear() => _breakpoints.Clear();

    public IReadOnlyList<BreakpointExpression> ToList() => _breakpoints.Values.OrderBy(breakpoint => breakpoint.Id).ToList();

    public BreakpointEvaluationResult EvaluateAndConsume(ref readonly BreakpointContext context) =>
        EvaluateAndConsume(context.GetMatchTargets());

    public BreakpointEvaluationResult EvaluateAndConsume(IEnumerable<string> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        IReadOnlyCollection<string> matchTargets = targets as IReadOnlyCollection<string> ?? [.. targets];
        KeyValuePair<int, BreakpointExpression>[] candidates = _breakpoints.ToArray();
        // One-shot breakpoints are evaluated first so step always applies to the next execution boundary, even when that module also matches an older
        // persistent breakpoint. Newer one-shots come first, matching front-of-queue insertion semantics.
        foreach ((int id, BreakpointExpression candidate) in candidates
            .OrderByDescending(static breakpoint => breakpoint.Value.IsOneShot)
            .ThenBy(static breakpoint => breakpoint.Value.IsOneShot ? -(long)breakpoint.Key : breakpoint.Key))
        {
            bool matched;
            try
            {
                matched = candidate.MatchesAny(matchTargets);
            }
            catch (RegexMatchTimeoutException exception)
            {
                if (candidate.IsOneShot && !_breakpoints.TryRemove(id, out _))
                {
                    continue;
                }

                DebugDiagnostic diagnostic = new(
                    DebugDiagnosticSeverity.Error,
                    $"Breakpoint {candidate.Id} expression '{candidate.Expression}' exceeded the regex match timeout: {exception.Message}");
                return BreakpointEvaluationResult.Faulted(candidate, diagnostic);
            }

            if (!matched)
            {
                continue;
            }

            if (candidate.IsOneShot && !_breakpoints.TryRemove(id, out _))
            {
                continue;
            }

            return BreakpointEvaluationResult.Match(candidate);
        }
        return BreakpointEvaluationResult.NoMatch;
    }

}
