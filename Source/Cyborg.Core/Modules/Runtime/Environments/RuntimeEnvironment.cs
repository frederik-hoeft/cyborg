using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Modules.Runtime.Environments;

public partial class RuntimeEnvironment(string name, bool isTransient) : IRuntimeEnvironment
{
    private readonly Dictionary<string, object?> _variables = [];

    public string Name => name;

    public bool IsTransient => isTransient;

    [GeneratedRegex(@"^\$(?<explicit_start>\{)?(?<variable_name>[A-Za-z_][A-Za-z_0-9\-]*)(?(explicit_start)\})$")]
    private static partial Regex VariableRegex { get; }

    public virtual bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value)
    {
        if (_variables.TryGetValue(name, out object? objValue))
        {
            if (objValue is string s && s is ['$', ..] && VariableRegex.Match(s) is { Success: true } match)
            {
                // Handle indirection via string variables
                string variableName = match.Groups["variable_name"].Value;
                return TryResolveVariable(variableName, out value);
            }
            if (objValue is T typedValue)
            {
                value = typedValue;
                return true;
            }
            throw new InvalidCastException($"Attempted to resolve variable '{name}' as type {typeof(T).FullName}, but it is of type {objValue?.GetType().FullName}.");
        }
        value = default;
        return false;
    }

    public virtual void SetVariable<T>(string name, T value) => _variables[name] = value;

    public virtual bool TryRemoveVariable(string name) => _variables.Remove(name);
}
