using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Core.Text;
using System.Collections;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Modules.Runtime.Environments;

public partial record EnvironmentLike(VariableSyntaxBuilder SyntaxFactory, string Namespace) : IEnvironmentLike
{
    private protected IEnvironmentVariableStore VariableStore { get; init; } = new MutableEnvironmentVariableStore();

    public ITaggedStringConversionObserver? TaggedStringConversionObserver { get; init; }

    protected JsonNamingPolicy NamingPolicy => SyntaxFactory.NamingPolicy;

    protected virtual TaggedString InterpolateString(ResolutionContext context, TaggedString tagged)
    {
        ArgumentNullException.ThrowIfNull(context);
        string stringValue = tagged.Value;
        if (!SyntaxFactory.InterpolationRegex.IsMatch(stringValue))
        {
            return tagged;
        }
        StringBuilder sb = new();
        ImmutableHashSet<string>.Builder tags = tagged.Tags.ToBuilder();
        int currentIndex = 0;
        ReadOnlySpan<char> valueSpan = stringValue.AsSpan();
        foreach (ValueMatch match in SyntaxFactory.InterpolationRegex.EnumerateMatches(stringValue))
        {
            sb.Append(valueSpan[currentIndex..match.Index]);
            ReadOnlySpan<char> variableSlice = valueSpan.Slice(match.Index, match.Length);
            string expression = variableSlice[2..^1].ToString();
            if (TryParseVariableReference(expression, out VariableReference reference) && TryResolveVariableReference(context, reference, out object? resolvedValue))
            {
                AppendResolvedInterpolationValue(sb, tags, resolvedValue);
            }
            else
            {
                // If the variable cannot be resolved, keep the original placeholder in the string
                sb.Append(variableSlice);
            }
            currentIndex = match.Index + match.Length;
        }
        sb.Append(valueSpan[currentIndex..]);
        return new TaggedString(sb.ToString(), tags.ToImmutable());
    }

    private static void AppendResolvedInterpolationValue(StringBuilder builder, ImmutableHashSet<string>.Builder tags, object? resolvedValue)
    {
        switch (resolvedValue)
        {
            case TaggedString tagged:
                builder.Append(tagged.Value);
                foreach (string tag in tagged.Tags)
                {
                    tags.Add(tag);
                }
                break;
            case string text:
                builder.Append(text);
                break;
            default:
                builder.Append(resolvedValue);
                break;
        }
    }

    protected virtual string FinalizeInterpolationLiterals(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SyntaxFactory.HashLiteralRegex.IsMatch(value))
        {
            return value;
        }

        StringBuilder builder = new();
        int currentIndex = 0;
        ReadOnlySpan<char> valueSpan = value.AsSpan();
        foreach (ValueMatch match in SyntaxFactory.HashLiteralRegex.EnumerateMatches(value))
        {
            builder.Append(valueSpan[currentIndex..match.Index]);
            ReadOnlySpan<char> literalSlice = valueSpan.Slice(match.Index, match.Length);
            builder.Append(literalSlice[..2]);
            builder.Append(literalSlice[3..]);
            currentIndex = match.Index + match.Length;
        }
        builder.Append(valueSpan[currentIndex..]);
        return builder.ToString();
    }

    protected virtual bool TryResolveIndirectionCandidate<T>(ResolutionContext context, string name, [NotNullWhen(true)] out T? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(name);
        if (name.StartsWith('$') && SyntaxFactory.IndirectionRegex.Match(name) is { Success: true } match)
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
        if (VariableStore.TryGetValue(context.Name, out object? objValue))
        {
            if (objValue is TaggedString tagged)
            {
                if (TryResolveIndirectionCandidate(context, tagged.Value, out object? redirected))
                {
                    value = UnionResolvedValue(redirected, tagged.Tags);
                    return true;
                }
                value = InterpolateString(context, tagged);
                return true;
            }
            // might need to resolve indirection via string variables, e.g. var1 = "${var2}", var2 = "actual_value"
            if (objValue is string s && TryResolveIndirectionCandidate(context, s, out value))
            {
                return true;
            }
            // handle interpolation within string variables, e.g. var1 = "Value is ${var2}", var2 = "actual_value"
            if (objValue is string stringValue)
            {
                TaggedString interpolated = InterpolateString(context, stringValue);
                value = interpolated.HasTags ? interpolated : interpolated.Value;
                return true;
            }
            value = objValue;
            return value is not null;
        }
        value = default;
        return false;
    }

    protected virtual bool TryGetStoredVariableInCurrentScopeCore(string name, [NotNullWhen(true)] out object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (VariableStore.TryGetValue(name, out value) && value is not null)
        {
            return true;
        }
        value = default;
        return false;
    }

    internal protected virtual bool TryGetStoredVariableRecursiveCore(string name, [NotNullWhen(true)] out object? value) =>
        TryGetStoredVariableInCurrentScopeCore(name, out value);

    internal protected virtual bool TryResolveVariableRecursiveCore(ResolutionContext context, [NotNullWhen(true)] out object? value) =>
        TryResolveVariableInCurrentScopeCore(context, out value);

    internal protected bool TryResolveVariableRecursiveCore<T>(ResolutionContext context, [NotNullWhen(true)] out T? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (TryResolveVariableRecursiveCore(context, out object? objValue))
        {
            if (TryConvertResolvedValue(objValue, context.Name, notifyImplicitConversion: true, out value))
            {
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
        if (!TryResolveVariable(name, entryPoint: this, out value))
        {
            return false;
        }
        if (value is string stringValue)
        {
            value = (T)(object)FinalizeInterpolationLiterals(stringValue);
        }
        else if (value is TaggedString tagged)
        {
            value = (T)(object)tagged.WithValue(FinalizeInterpolationLiterals(tagged.Value));
        }
        return true;
    }

    public virtual void SetVariable<T>(string name, T value) => VariableStore.SetValue(name, value);

    public virtual bool TryRemoveVariable(string name) => VariableStore.TryRemove(name);

    public virtual TaggedString Interpolate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return InterpolateCore(template, entryPoint: this);
    }

    public virtual TaggedString Interpolate(TaggedString template) => InterpolateCore(template, entryPoint: this);

    public virtual TaggedString Interpolate(TaggedString? template) =>
        template is { } tagged ? InterpolateCore(tagged, entryPoint: this) : default;

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

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => VariableStore.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private protected bool TryGetStoredVariable<T>(string name, [NotNullWhen(true)] out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (TryGetStoredVariableRecursiveCore(name, out object? objectValue))
        {
            if (TryConvertResolvedValue(objectValue, name, notifyImplicitConversion: true, out value))
            {
                return true;
            }
            throw new InvalidCastException($"Attempted to select stored variable '{name}' as type {typeof(T).FullName}, but it is of type {objectValue.GetType().FullName}.");
        }
        value = default;
        return false;
    }

    private protected bool TryResolveVariable<T>(string name, EnvironmentLike entryPoint, [NotNullWhen(true)] out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(entryPoint);
        return TryResolveVariableRecursiveCore(ResolutionContext.Create(entryPoint, name), out value);
    }

    private protected TaggedString InterpolateCore(string template, EnvironmentLike entryPoint) =>
        InterpolateCore(new TaggedString(template), entryPoint);

    private protected TaggedString InterpolateCore(TaggedString template, EnvironmentLike entryPoint)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        TaggedString interpolated = InterpolateString(ResolutionContext.CreateRoot(entryPoint), template);
        return interpolated.WithValue(FinalizeInterpolationLiterals(interpolated.Value));
    }

    private bool TryConvertResolvedValue<T>(object? objValue, string variableName, bool notifyImplicitConversion, [NotNullWhen(true)] out T? value)
    {
        if (objValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        if (typeof(T) == typeof(string) && objValue is TaggedString tagged)
        {
            if (notifyImplicitConversion && tagged.HasTags)
            {
                TaggedStringConversionObserver?.OnImplicitStringRetrieval(variableName, tagged);
            }
            value = (T)(object)tagged.Value;
            return true;
        }
        if (typeof(T) == typeof(TaggedString) && objValue is string text)
        {
            value = (T)(object)new TaggedString(text);
            return true;
        }
        value = default;
        return false;
    }

    private static object UnionResolvedValue(object resolvedValue, ImmutableHashSet<string> extraTags)
    {
        if (extraTags.IsEmpty)
        {
            return resolvedValue;
        }
        if (resolvedValue is TaggedString tagged)
        {
            return tagged.WithTags(extraTags);
        }
        if (resolvedValue is string text)
        {
            return new TaggedString(text, extraTags);
        }
        return resolvedValue;
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
