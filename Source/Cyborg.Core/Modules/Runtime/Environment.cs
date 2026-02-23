using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime;

public class Environment(string name) : IEnvironment
{
    private readonly Dictionary<string, object?> _variables = [];

    public string Name => name;

    public bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value)
    {
        if (_variables.TryGetValue(name, out object? objValue) && objValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    public void SetVariable<T>(string name, T value) => _variables[name] = value;

    public bool TryRemoveVariable(string name) => _variables.Remove(name);
}
