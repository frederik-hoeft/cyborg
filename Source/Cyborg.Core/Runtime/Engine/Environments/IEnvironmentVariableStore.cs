namespace Cyborg.Core.Runtime.Engine.Environments;

internal interface IEnvironmentVariableStore : IEnumerable<KeyValuePair<string, object?>>
{
    bool TryGetValue(string name, out object? value);

    void SetValue(string name, object? value);

    bool TryRemove(string name);
}
