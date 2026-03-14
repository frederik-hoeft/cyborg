using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Modules.Runtime.Environments;

public partial record EnvironmentLike(VariableSyntaxBuilder SyntaxFactory, string Namespace) : IEnvironmentLike
{
    protected Dictionary<string, object?> Variables { get; } = [];

    protected JsonNamingPolicy NamingPolicy => SyntaxFactory.NamingPolicy;

    [GeneratedRegex(@"^\$\{(?<variable_name>([A-Za-z_][A-Za-z_0-9\-\.]*)|@)\}$")]
    protected static partial Regex VariableRegex { get; }

    [GeneratedRegex(@"\$\{(?<variable_name>([A-Za-z_][A-Za-z_0-9\-\.]*)|@)\}")]
    protected static partial Regex InterpolationRegex { get; }

    protected virtual string InterpolateString(ResolutionContext context, string stringValue)
    {
        ArgumentNullException.ThrowIfNull(context);
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
            if (TryResolveVariableCore(context.With(variableName), out object? resolvedValue))
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

    protected virtual bool TryResolveVariableCandidate<T>(ResolutionContext context, string name, [NotNullWhen(true)] out T? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(name);
        if (name.StartsWith('$') && VariableRegex.Match(name) is { Success: true } match)
        {
            string variable = match.Groups["variable_name"].Value;
            return TryResolveVariableCore(context.With(variable), out value);
        }
        value = default;
        return false;
    }

    protected virtual bool TryResolveVariableCore(ResolutionContext context, [NotNullWhen(true)] out object? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        // handle self-reference
        if (context.Name.Equals(SyntaxFactory.Self(), StringComparison.Ordinal))
        {
            value = Namespace;
            return true;
        }
        if (Variables.TryGetValue(context.Name, out object? objValue))
        {
            // might need to resolve indirection via string variables, e.g. var1 = "${var2}", var2 = "actual_value"
            if (objValue is string s && TryResolveVariableCandidate(context, s, out value))
            {
                return true;
            }
            // handle interpolation within string variables, e.g. var1 = "Value is ${var2}", var2 = "actual_value"
            if (objValue is string stringValue)
            {
                value = InterpolateString(context, stringValue);
                return true;
            }
            value = objValue;
            return value is not null;
        }
        value = default;
        return false;
    }

    private protected virtual bool TryResolveVariableCore<T>(ResolutionContext context, [NotNullWhen(true)] out T? value)
    {
        if (TryResolveVariableCore(context, out object? objValue))
        {
            if (objValue is T typedValue)
            {
                value = typedValue;
                return true;
            }
            throw new InvalidCastException($"Attempted to resolve variable '{context.Name}' as type {typeof(T).FullName}, but it is of type {objValue?.GetType().FullName}.");
        }
        value = default;
        return false;
    }

    public virtual bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value) => TryResolveVariableCore(ResolutionContext.Create(name), out value);

    public virtual void SetVariable<T>(string name, T value) => Variables[name] = value;

    public virtual bool TryRemoveVariable(string name) => Variables.Remove(name);

    public virtual string Interpolate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return InterpolateString(ResolutionContext.Empty, template);
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
                    SetVariable(SyntaxFactory.Path(root, key), nested);
                }
                if (strategy is not DecompositionStrategy.Shallow)
                {
                    Publish(SyntaxFactory.Path(root, key), nested, strategy, publishNullValues);
                }
            }
            else if (value is not null || publishNullValues)
            {
                // leaf node
                SetVariable(SyntaxFactory.Path(root, key), value);
            }
        }
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => Variables.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected sealed class ResolutionContext
    {
        private readonly ResolutionContext? _parent;

        public string Name { get; }

        private ResolutionContext(ResolutionContext? parent, string name)
        {
            _parent = parent;
            Name = name;
        }

        public static ResolutionContext Create(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return new ResolutionContext(parent: null, name);
        }

        public static ResolutionContext Empty { get; } = new ResolutionContext(parent: null, name: string.Empty);

        public ResolutionContext With(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            for (ResolutionContext? current = this; current is not null; current = current._parent)
            {
                if (current.Name.Equals(name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Cyclic variable reference detected for variable '{name}'.");
                }
            }
            return new ResolutionContext(this, name);
        }
    }
}