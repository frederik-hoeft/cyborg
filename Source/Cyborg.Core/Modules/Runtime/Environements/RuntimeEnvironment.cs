using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Environements;

public class RuntimeEnvironment(string name, bool isTransient) : IRuntimeEnvironment
{
    private readonly Dictionary<string, object?> _variables = [];

    public string Name => name;

    public bool IsTransient => isTransient;

    public virtual bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value)
    {
        if (_variables.TryGetValue(name, out object? objValue) && objValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    public virtual void SetVariable<T>(string name, T value) => _variables[name] = value;

    public virtual bool TryRemoveVariable(string name) => _variables.Remove(name);
}
