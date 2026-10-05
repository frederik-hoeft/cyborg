using System.Collections;

namespace Cyborg.Core.Runtime.Engine.Environments;

internal sealed class MutableEnvironmentVariableStore : IEnvironmentVariableStore
{
    private readonly Dictionary<string, object?> _variables = [];
    private int _nextCollectionOrderSlot;

    public bool TryGetValue(string name, out object? value) => _variables.TryGetValue(name, out value);

    public void SetValue(string name, object? value) => _variables[name] = value;

    public bool TryRemove(string name) => _variables.Remove(name);

    public string AllocateCollectionOrderToken()
    {
        // '!' sorts before the digit-prefixed tokens used by transactional stores, and never collides with them.
        string token = "!" + _nextCollectionOrderSlot.ToString("D10");
        _nextCollectionOrderSlot++;
        return token;
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _variables.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
