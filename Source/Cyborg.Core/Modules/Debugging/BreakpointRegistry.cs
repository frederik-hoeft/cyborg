namespace Cyborg.Core.Modules.Debugging;

public sealed class BreakpointRegistry : IBreakpointRegistry
{
    // TODO: use Lock object or thread-safe collection instead of manual locking, maybe use ConcurrentDictionary<int, BreakpointExpression> for O(1) lookup by id and O(n) enumeration for matching.
    private readonly object _gate = new();
    private readonly List<BreakpointExpression> _breakpoints = [];
    private int _nextId = 1;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _breakpoints.Count;
            }
        }
    }

    public int Add(string expression, bool isOneShot = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        lock (_gate)
        {
            int id = _nextId++;
            BreakpointExpression breakpoint = new(id, expression, isOneShot);
            _breakpoints.Add(breakpoint);
            return id;
        }
    }

    public bool Remove(int id)
    {
        lock (_gate)
        {
            for (int i = 0; i < _breakpoints.Count; ++i)
            {
                if (_breakpoints[i].Id == id)
                {
                    _breakpoints.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _breakpoints.Clear();
        }
    }

    public IReadOnlyList<BreakpointExpression> List()
    {
        lock (_gate)
        {
            return _breakpoints.ToArray();
        }
    }

    public bool TryMatchAndConsume(string moduleId, string? name, string? group, out BreakpointExpression? matched)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        lock (_gate)
        {
            for (int i = 0; i < _breakpoints.Count; ++i)
            {
                BreakpointExpression candidate = _breakpoints[i];
                if (!candidate.Matches(moduleId, name, group))
                {
                    continue;
                }

                matched = candidate;
                if (candidate.IsOneShot)
                {
                    _breakpoints.RemoveAt(i);
                }
                // TODO: should we break greedily and return on first match, or evaluate all breakpoints and return the first match? For now, we return on first match.
                return true;
            }

            matched = null;
            return false;
        }
    }
}
