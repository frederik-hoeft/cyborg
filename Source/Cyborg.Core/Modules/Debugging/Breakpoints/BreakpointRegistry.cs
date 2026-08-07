using System.Collections.Concurrent;

namespace Cyborg.Core.Modules.Debugging.Breakpoints;

public sealed class BreakpointRegistry : IBreakpointRegistry
{
    private readonly ConcurrentDictionary<int, BreakpointExpression> _breakpoints = [];
    private int _lastId = 0;

    public int Count => _breakpoints.Count;

    public int Add(string expression, bool isOneShot = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        int id = Interlocked.Increment(ref _lastId);

        BreakpointExpression breakpoint = new(id, expression, isOneShot);
        if (!_breakpoints.TryAdd(id, breakpoint))
        {
            // this should never happen, but if you manage to overflow the counter, this will throw on duplicate keys
            throw new InvalidOperationException($"Failed to add breakpoint with id {id}.");
        }
        return id;
    }

    public bool Remove(int id) => _breakpoints.TryRemove(id, out _);

    public void Clear() => _breakpoints.Clear();

    public IReadOnlyList<BreakpointExpression> List() => _breakpoints.Values.OrderBy(b => b.Id).ToList();

    public bool TryMatchAndConsume(ref readonly BreakpointContext context, out BreakpointExpression? matched) =>
        TryMatchAndConsume(context.GetMatchTargets(), out matched);

    public bool TryMatchAndConsume(IEnumerable<string> targets, out BreakpointExpression? matched)
    {
        ArgumentNullException.ThrowIfNull(targets);
        // ConcurrentDictionary supports snapshot enumeration, so this is fine
        foreach ((int id, BreakpointExpression candidate) in _breakpoints.OrderBy(static kvp => kvp.Key))
        {
            if (!candidate.MatchesAny(targets))
            {
                continue;
            }

            matched = candidate;
            if (candidate.IsOneShot)
            {
                _breakpoints.TryRemove(id, out _);
            }
            return true;
        }
        matched = null;
        return false;
    }
}
