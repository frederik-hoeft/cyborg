using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Modules.Runtime.Environments;

public partial class RuntimeEnvironment(string name, bool isTransient, JsonNamingPolicy namingPolicy) : IRuntimeEnvironment
{
    private readonly Dictionary<string, object?> _variables = [];

    public string Name => name;

    public bool IsTransient => isTransient;

    [GeneratedRegex(@"^\$\{(?<variable_name>([A-Za-z_][A-Za-z_0-9\-\.]*)|@)\}$")]
    private static partial Regex VariableRegex { get; }

    [GeneratedRegex(@"\$\{(?<variable_name>([A-Za-z_][A-Za-z_0-9\-\.]*)|@)\}")]
    private static partial Regex InterpolationRegex { get; }

    public string Self => "@";

    public virtual string? EffectiveNamespace => TryResolveVariable(Self, out string? selfReference) ? selfReference : null;

    public VariableSyntaxFactory SyntaxFactory => field ??= new VariableSyntaxFactory(this, namingPolicy);

    public virtual string Interpolate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return InterpolateString(template);
    }

    protected virtual string InterpolateString(string stringValue)
    {
        if (!InterpolationRegex.IsMatch(stringValue))
        {
            return stringValue;
        }
        StringBuilder sb = new();
        int currentIndex = 0;
        ReadOnlySpan<char> valueSpan = stringValue.AsSpan();
        foreach (ValueMatch match in InterpolationRegex.EnumerateMatches(stringValue))
        {
            sb.Append(valueSpan[currentIndex..match.Index]);
            ReadOnlySpan<char> variableSlice = valueSpan.Slice(match.Index, match.Length);
            string variableName = variableSlice[2..^1].ToString();
            if (TryResolveVariableCore(variableName, out object? resolvedValue))
            {
                sb.Append(resolvedValue);
            }
            else
            {
                // If the variable cannot be resolved, keep the original placeholder in the string
                sb.Append(variableSlice);
            }
            currentIndex = match.Index + match.Length;
        }
        sb.Append(valueSpan[currentIndex..]);
        return sb.ToString();
    }

    protected virtual bool TryResolveVariableCandidate<T>(string name, [NotNullWhen(true)] out T? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.StartsWith('$') && VariableRegex.Match(name) is { Success: true } match)
        {
            string variableName = match.Groups["variable_name"].Value;
            return TryResolveVariable(variableName, out value);
        }
        value = default;
        return false;
    }

    protected virtual bool TryResolveVariableCore(string name, [NotNullWhen(true)] out object? value)
    {
        if (_variables.TryGetValue(name, out object? objValue))
        {
            if (objValue is string s && TryResolveVariableCandidate(s, out value))
            {
                return true;
            }
            if (objValue is string stringValue)
            {
                value = InterpolateString(stringValue);
                return true;
            }
            value = objValue;
            return value is not null;
        }
        value = default;
        return false;
    }

    public virtual bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value)
    {
        if (TryResolveVariableCore(name, out object? objValue))
        {
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

    [return: NotNullIfNotNull(nameof(value))]
    protected virtual T? Resolve<TModule, T>(TModule module, T? value, [CallerArgumentExpression(nameof(module))] string? moduleExpression = null, [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
        where TModule : class, IModule
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueExpression);
        if (!valueExpression.StartsWith(moduleExpression, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The value must be provided as a member access expression (e.g. 'MyModule.MyProperty'). Provided value: '{valueExpression}' does not match the expected format.", nameof(value));
        }
        ReadOnlySpan<char> valueSpan = valueExpression.AsSpan()[moduleExpression.Length..];
        if (valueSpan is not ['.', ..] and not ['?', '.', ..])
        {
            throw new ArgumentException($"The value must be provided as a member access expression (e.g. 'MyModule.MyProperty'). Provided value: '{valueExpression}' does not match the expected format.", nameof(value));
        }
        Span<char> cleanedSpan = stackalloc char[valueSpan.Length - 1];
        int skippedChars = 1;
        for (int i = 1; i < valueSpan.Length; i++)
        {
            char c = valueSpan[i];
            if (c == '?')
            {
                skippedChars++;
                continue;
            }
            cleanedSpan[i - skippedChars] = c;
        }
        string valuePath = namingPolicy.ConvertName(valueSpan.Slice(1, valueSpan.Length - skippedChars).ToString());
        if (!string.IsNullOrEmpty(module.Name) && TryResolveVariable(SyntaxFactory.Override(SyntaxFactory.Variable(module.Name, valuePath)).Render(), out T? resolvedValue)
            || TryResolveVariable(SyntaxFactory.Override(SyntaxFactory.Variable(TModule.ModuleId,valuePath)).Render(), out resolvedValue))
        {
            value = resolvedValue;
        }
        else if (value is string stringValue)
        {
            // Handle indirection via string variables
            string resolvedString = InterpolateString(stringValue);
            value = Unsafe.As<string, T>(ref resolvedString);
        }
        return value;
    }

    public virtual string GetEffectiveNamespace<TModule>(TModule module) where TModule : class, IModule
    {
        ArgumentNullException.ThrowIfNull(module);
        return GetEffectiveNamespace(module.Name, TModule.ModuleId);
    }

    public virtual string GetEffectiveNamespace(IModuleWorker module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return GetEffectiveNamespace(module.Module.Name, module.ModuleId);
    }

    private static string GetEffectiveNamespace(string? name, string moduleId) => !string.IsNullOrEmpty(name) ? name : moduleId;

    SelfReferenceScope IRuntimeEnvironment.EnterSelfReferenceScope(IModuleWorker module)
    {
        _ = TryResolveVariable(Self, out string? selfReference);
        SelfReferenceScope scope = new(this, selfReference);
        SetVariable(Self, GetEffectiveNamespace(module.Module.Name, module.ModuleId));
        return scope;
    }

    T? IRuntimeEnvironment.Resolve<TModule, T>(TModule module, T? value, string? moduleExpression, string? valueExpression) where T : default => 
        Resolve(module, value, moduleExpression, valueExpression);

    void IRuntimeEnvironment.Publish<TModule, T>(TModule module, string root, T decomposable)
    {
        ArgumentNullException.ThrowIfNull(module);
        Publish(root, decomposable, module.Artifacts.DecompositionStrategy, module.Artifacts.PublishNullValues);
    }

    public virtual void Publish(string root, IDecomposable decomposable, DecompositionStrategy strategy, bool publishNullValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(decomposable);

        if (strategy is DecompositionStrategy.FullHierarchy)
        {
            SetVariable(root, decomposable);
        }
        foreach ((string key, object? value) in decomposable.Decompose())
        {
            if (value is IDecomposable nested)
            {
                // inner node
                if (strategy is not DecompositionStrategy.LeavesOnly)
                {
                    SetVariable(SyntaxFactory.Variable(root, key).Render(), nested);
                }
                if (strategy is not DecompositionStrategy.Shallow)
                {
                    Publish(SyntaxFactory.Variable(root, key).Render(), nested, strategy, publishNullValues);
                }
            }
            else if (value is not null || publishNullValues)
            {
                // leaf node
                SetVariable(SyntaxFactory.Variable(root, key).Render(), value);
            }
        }
    }
}
