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

    protected virtual string InterpolateString(ResolutionContext context, string stringValue)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!SyntaxFactory.InterpolationRegex.IsMatch(stringValue))
        {
            return stringValue;
        }
        StringBuilder sb = new();
        int currentIndex = 0;
        ReadOnlySpan<char> valueSpan = stringValue.AsSpan();
        foreach (ValueMatch match in SyntaxFactory.InterpolationRegex.EnumerateMatches(stringValue))
        {
            sb.Append(valueSpan[currentIndex..match.Index]);
            ReadOnlySpan<char> variableSlice = valueSpan.Slice(match.Index, match.Length);
            string expression = variableSlice[2..^1].ToString();
            if (TryParseVariableReference(expression, out VariableReference reference) && TryResolveVariableReference(context, reference, out object? resolvedValue))
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
        if (name.StartsWith('$') && SyntaxFactory.VariableRegex.Match(name) is { Success: true } match)
        {
            string expression = match.Groups["expression"].Value;
            if (TryParseVariableReference(expression, out VariableReference reference))
            {
                return TryResolveVariableReference(context, reference, out value);
            }
        }
        value = default;
        return false;
    }

    protected virtual bool TryResolveVariableInCurrentScopeCore(ResolutionContext context, [NotNullWhen(true)] out object? value)
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

    internal protected virtual bool TryResolveVariableRecursiveCore(ResolutionContext context, [NotNullWhen(true)] out object? value) =>
        TryResolveVariableInCurrentScopeCore(context, out value);

    internal protected bool TryResolveVariableRecursiveCore<T>(ResolutionContext context, [NotNullWhen(true)] out T? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (TryResolveVariableRecursiveCore(context, out object? objValue))
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

    protected bool TryResolveVariableReference<T>(ResolutionContext context, VariableReference reference, [NotNullWhen(true)] out T? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        ResolutionContext nextContext = context.With(reference.Name, reference.Origin);
        return reference.Origin switch
        {
            ResolutionOrigin.CurrentScope => TryResolveVariableRecursiveCore(nextContext, out value),
            ResolutionOrigin.EntryPoint => context.EntryPoint.TryResolveVariableRecursiveCore(nextContext, out value),
            _ => throw new ArgumentOutOfRangeException(nameof(reference))
        };
    }

    public virtual bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return TryResolveVariable(name, entryPoint: this, out value);
    }

    public virtual void SetVariable<T>(string name, T value) => Variables[name] = value;

    public virtual bool TryRemoveVariable(string name) => Variables.Remove(name);

    public virtual string Interpolate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return Interpolate(template, entryPoint: this);
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

    private protected bool TryResolveVariable<T>(string name, EnvironmentLike entryPoint, [NotNullWhen(true)] out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(entryPoint);
        return TryResolveVariableRecursiveCore(ResolutionContext.Create(entryPoint, name), out value);
    }

    private protected string Interpolate(string template, EnvironmentLike entryPoint)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(entryPoint);
        return InterpolateString(ResolutionContext.CreateRoot(entryPoint), template);
    }

    private bool TryParseVariableReference(string expression, out VariableReference reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        string self = SyntaxFactory.Self();
        if (expression.Equals(self, StringComparison.Ordinal))
        {
            reference = new VariableReference(self, ResolutionOrigin.CurrentScope);
            return true;
        }
        if (expression.Equals(LateRefSyntax.UncheckedMakeLate(SyntaxFactory.Self()), StringComparison.Ordinal))
        {
            reference = new VariableReference(self, ResolutionOrigin.EntryPoint);
            return true;
        }
        if (expression.StartsWith(LateRefSyntax.Symbol, StringComparison.Ordinal))
        {
            reference = new VariableReference(expression[LateRefSyntax.Symbol.Length..], ResolutionOrigin.EntryPoint);
            return true;
        }
        reference = new VariableReference(expression, ResolutionOrigin.CurrentScope);
        return true;
    }

    protected readonly record struct VariableReference(string Name, ResolutionOrigin Origin);

    internal protected enum ResolutionOrigin
    {
        CurrentScope,
        EntryPoint
    }

    internal protected sealed class ResolutionContext
    {
        private readonly ResolutionContext? _parent;

        public EnvironmentLike EntryPoint { get; }

        public string Name { get; }

        public ResolutionOrigin Origin { get; }

        private ResolutionContext(ResolutionContext? parent, EnvironmentLike entryPoint, string name, ResolutionOrigin origin)
        {
            _parent = parent;
            EntryPoint = entryPoint;
            Name = name;
            Origin = origin;
        }

        public static ResolutionContext Create(EnvironmentLike entryPoint, string name)
        {
            ArgumentNullException.ThrowIfNull(entryPoint);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return new ResolutionContext(parent: null, entryPoint, name, ResolutionOrigin.CurrentScope);
        }

        public static ResolutionContext CreateRoot(EnvironmentLike entryPoint)
        {
            ArgumentNullException.ThrowIfNull(entryPoint);
            return new ResolutionContext(parent: null, entryPoint, name: string.Empty, ResolutionOrigin.CurrentScope);
        }

        public ResolutionContext With(string name, ResolutionOrigin origin)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            for (ResolutionContext? current = this; current is not null; current = current._parent)
            {
                if (current.Name.Equals(name, StringComparison.Ordinal) && current.Origin == origin)
                {
                    throw new InvalidOperationException($"Cyclic variable reference detected for variable '{FormatReference(name, origin)}'.");
                }
            }
            return new ResolutionContext(this, EntryPoint, name, origin);
        }

        private static string FormatReference(string name, ResolutionOrigin origin)
            => origin is ResolutionOrigin.EntryPoint ? LateRefSyntax.UncheckedMakeLateRef(name) : RefSyntax.UncheckedMakeRef(name);
    }
}
