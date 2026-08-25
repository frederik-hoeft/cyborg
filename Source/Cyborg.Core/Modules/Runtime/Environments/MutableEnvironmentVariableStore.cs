using System.Collections;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal sealed class MutableEnvironmentVariableStore : IEnvironmentVariableStore
{
    private readonly Dictionary<string, object?> _variables = [];

    public bool TryGetValue(string name, out object? value) => _variables.TryGetValue(name, out value);

    public void SetValue(string name, object? value) => _variables[name] = value;

    public bool TryRemove(string name) => _variables.Remove(name);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _variables.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
