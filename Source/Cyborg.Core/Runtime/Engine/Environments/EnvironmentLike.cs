using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Runtime.Engine.Environments.Artifacts;
using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Text;
using System.Collections;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Runtime.Engine.Environments;

public partial record EnvironmentLike(VariableSyntaxBuilder SyntaxFactory, string Namespace) : IEnvironmentLike
{
    private protected IEnvironmentVariableStore VariableStore { get; init; } = new MutableEnvironmentVariableStore();

    internal ITaggedStringConversionObserver? TaggedStringConversionObserver { get; init; }

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
            if (TryParseVariableReference(expression, out VariableReference reference) && TryResolveVariableReference(context, reference, out Evaluation resolved))
            {
                object? splice = resolved.Terminal && resolved.Value is not null
                    ? ExpressionShield.ShieldText(resolved.Value)
                    : resolved.Value;
                AppendResolvedInterpolationValue(sb, tags, splice);
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

    internal protected virtual bool TryResolveVariableInCurrentScopeCore(ResolutionContext context, out Evaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Name.Equals(SyntaxFactory.Self(), StringComparison.Ordinal))
        {
            evaluation = Evaluation.Of(Namespace);
            return true;
        }
        if (VariableStore.TryGetValue(context.Name, out object? objValue) && objValue is not null)
        {
            if (objValue is CapturedValue captured)
            {
                evaluation = Evaluation.TerminalValue(captured.Value);
                return captured.Value is not null;
            }
            if (objValue is TaggedString tagged)
            {
                return TryEvaluateStoredText(context, tagged.Value, tagged.Tags, out evaluation);
            }
            if (objValue is string text)
            {
                return TryEvaluateStoredText(context, text, wrapperTags: null, out evaluation);
            }
            evaluation = Evaluation.Of(objValue);
            return true;
        }
        evaluation = default;
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

    internal protected virtual bool TryResolveVariableRecursiveCore(ResolutionContext context, out Evaluation evaluation) =>
        TryResolveVariableInCurrentScopeCore(context, out evaluation);

    private bool TryResolveVariableReference(ResolutionContext context, VariableReference reference, out Evaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ResolutionContext nextContext = context.With(reference.Name, reference.Origin);
        return reference.Origin switch
        {
            ResolutionOrigin.CurrentScope => TryResolveVariableRecursiveCore(nextContext, out evaluation),
            ResolutionOrigin.EntryPoint => context.EntryPoint.TryResolveVariableRecursiveCore(nextContext, out evaluation),
            _ => throw new ArgumentOutOfRangeException(nameof(reference))
        };
    }

    public virtual bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!TryResolveEvaluation(name, entryPoint: this, out Evaluation evaluation) || evaluation.Value is null)
        {
            value = default;
            return false;
        }
        object resolved = evaluation.Terminal ? evaluation.Value : FinalizeIfText(evaluation.Value);
        if (TryConvertResolvedValue(resolved, name, notifyImplicitConversion: true, out value))
        {
            return true;
        }
        throw new InvalidCastException($"Attempted to resolve variable '{name}' as type {typeof(T).FullName}, but it is of type {resolved.GetType().FullName}.");
    }

    public virtual void SetVariable<T>(string name, T value) => VariableStore.SetValue(name, PrepareStoredValue(value));

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

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        foreach ((string key, object? value) in VariableStore)
        {
            yield return new KeyValuePair<string, object?>(key, value is CapturedValue captured ? captured.Value : value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Copies stored values without passing them through public value enumeration or parsing. Captured values retain their terminal marker,
    /// and ordinary expression strings remain unevaluated in the destination environment.
    /// </summary>
    internal void CopyStoredVariablesTo(EnvironmentLike destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (ReferenceEquals(this, destination))
        {
            return;
        }
        foreach ((string key, object? value) in VariableStore)
        {
            destination.VariableStore.SetValue(key, value);
        }
    }

    private protected bool TryGetStoredVariable<T>(string name, bool shieldInterpolation, [NotNullWhen(true)] out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (TryGetStoredVariableRecursiveCore(name, out object? objectValue))
        {
            if (objectValue is CapturedValue captured)
            {
                if (captured.Value is null)
                {
                    value = default;
                    return false;
                }
                objectValue = shieldInterpolation
                    ? ExpressionShield.ShieldText(captured.Value)
                    : ExpressionShield.ShieldValueExpressions(captured.Value);
            }
            if (TryConvertResolvedValue(objectValue, name, notifyImplicitConversion: true, out value))
            {
                return true;
            }
            throw new InvalidCastException($"Attempted to select stored variable '{name}' as type {typeof(T).FullName}, but it is of type {objectValue.GetType().FullName}.");
        }
        value = default;
        return false;
    }

    private protected bool TryResolveEvaluation(string name, EnvironmentLike entryPoint, out Evaluation evaluation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(entryPoint);
        return TryResolveVariableRecursiveCore(ResolutionContext.Create(entryPoint, name), out evaluation);
    }

    private protected TaggedString InterpolateCore(string template, EnvironmentLike entryPoint) =>
        InterpolateCore(new TaggedString(template), entryPoint);

    private protected TaggedString InterpolateCore(TaggedString template, EnvironmentLike entryPoint)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        EnsureTextual(template.Value);
        TaggedString interpolated = InterpolateString(ResolutionContext.CreateRoot(entryPoint), template);
        return interpolated.WithValue(FinalizeInterpolationLiterals(interpolated.Value));
    }

    private protected bool TryConvertResolvedValue<T>(object? objValue, string variableName, bool notifyImplicitConversion, [NotNullWhen(true)] out T? value)
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

    private bool TryEvaluateStoredText(ResolutionContext context, string text, ImmutableHashSet<string>? wrapperTags, out Evaluation evaluation)
    {
        ValueExpression expression = ValueExpressionParser.Parse(SyntaxFactory, text);
        switch (expression.Kind)
        {
            case ValueExpressionKind.LazyIndirection:
            {
                if (!TryParseVariableReference(expression.Expression, out VariableReference reference)
                    || !TryResolveVariableReference(context, reference, out Evaluation target)
                    || target.Value is null)
                {
                    throw new InvalidOperationException($"Failed to resolve indirection '&{{{expression.Expression}}}' for variable '{context.Name}' because the target is not defined.");
                }
                evaluation = WithWrapperTags(target, wrapperTags);
                return true;
            }
            case ValueExpressionKind.EagerCapture:
                throw new InvalidOperationException($"Eager capture '*{{{expression.Expression}}}' in variable '{context.Name}' was not evaluated when the variable was defined.");
            case ValueExpressionKind.Text:
            {
                TaggedString template = wrapperTags is null ? new TaggedString(text) : new TaggedString(text, wrapperTags);
                TaggedString interpolated = InterpolateString(context, template);
                object result = interpolated.HasTags ? interpolated : interpolated.Value;
                evaluation = Evaluation.Of(result);
                return true;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(text), expression.Kind, "Unsupported value expression kind.");
        }
    }

    private object? PrepareStoredValue<T>(T value)
    {
        if (value is null)
        {
            return null;
        }
        if (value is string text)
        {
            return PrepareStoredText(text, tags: null);
        }
        if (value is TaggedString tagged)
        {
            return PrepareStoredText(tagged.Value, tagged.Tags);
        }
        return value;
    }

    private object PrepareStoredText(string text, ImmutableHashSet<string>? tags)
    {
        ValueExpression expression = ValueExpressionParser.Parse(SyntaxFactory, text);
        if (expression.Kind != ValueExpressionKind.EagerCapture)
        {
            if (tags is not null)
            {
                return new TaggedString(text, tags);
            }
            return text;
        }

        if (!TryResolveVariable(expression.Expression, out object? snapshot))
        {
            throw new InvalidOperationException($"Failed to capture '*{{{expression.Expression}}}' because '{expression.Expression}' is not defined.");
        }
        object stored = tags is { IsEmpty: false } ? UnionResolvedValue(snapshot, tags) : snapshot;
        return new CapturedValue(stored);
    }

    private protected string ResolveValueExpressionCore(string value, bool willInterpolate)
    {
        Evaluation evaluation = EvaluateStandaloneValueExpression(value, wrapperTags: null, willInterpolate);
        if (evaluation.Value is null || !TryConvertResolvedValue(evaluation.Value, "value expression", notifyImplicitConversion: true, out string? resolved))
        {
            throw new InvalidCastException($"Value expression '{value}' did not resolve to {typeof(string).FullName}.");
        }
        return evaluation.Terminal && willInterpolate ? (string)ExpressionShield.ShieldText(resolved) : resolved;
    }

    private protected TaggedString ResolveValueExpressionCore(TaggedString value, bool willInterpolate)
    {
        Evaluation evaluation = EvaluateStandaloneValueExpression(value.Value, value.Tags, willInterpolate);
        if (evaluation.Value is null || !TryConvertResolvedValue(evaluation.Value, "value expression", notifyImplicitConversion: true, out TaggedString resolved))
        {
            throw new InvalidCastException($"Value expression '{value.Value}' did not resolve to {typeof(TaggedString).FullName}.");
        }
        return evaluation.Terminal && willInterpolate ? (TaggedString)ExpressionShield.ShieldText(resolved) : resolved;
    }

    private Evaluation EvaluateStandaloneValueExpression(string text, ImmutableHashSet<string>? wrapperTags, bool willInterpolate)
    {
        ValueExpression expression = ValueExpressionParser.Parse(SyntaxFactory, text);
        if (expression.Kind == ValueExpressionKind.Text)
        {
            string preparedText = willInterpolate ? text : ExpressionShield.FinalizeValueExpressionLiterals(text);
            object prepared = wrapperTags is null ? preparedText : new TaggedString(preparedText, wrapperTags);
            return Evaluation.Of(prepared);
        }

        if (!TryParseVariableReference(expression.Expression, out VariableReference reference)
            || !TryResolveVariableReference(ResolutionContext.CreateRoot(this), reference, out Evaluation target)
            || target.Value is null)
        {
            string operation = expression.Kind == ValueExpressionKind.LazyIndirection ? "indirection" : "capture";
            char symbol = expression.Kind == ValueExpressionKind.LazyIndirection ? '&' : '*';
            throw new InvalidOperationException($"Failed to resolve {operation} '{symbol}{{{expression.Expression}}}' because the target is not defined.");
        }

        Evaluation taggedTarget = WithWrapperTags(target, wrapperTags);
        object resolved = taggedTarget.Terminal ? taggedTarget.Value! : FinalizeIfText(taggedTarget.Value!);
        return Evaluation.TerminalValue(resolved);
    }

    private void EnsureTextual(string text)
    {
        ValueExpression expression = ValueExpressionParser.Parse(SyntaxFactory, text);
        if (expression.Kind == ValueExpressionKind.Text)
        {
            return;
        }
        throw new FormatException(
            $"Textual interpolation cannot evaluate '{text}'. Lazy indirection '&{{...}}' and eager capture '*{{...}}' are value operations and cannot appear in text or keys.");
    }

    private object FinalizeIfText(object value)
    {
        if (value is string text)
        {
            return FinalizeInterpolationLiterals(text);
        }
        if (value is TaggedString tagged)
        {
            return tagged.WithValue(FinalizeInterpolationLiterals(tagged.Value));
        }
        return value;
    }

    private static Evaluation WithWrapperTags(Evaluation target, ImmutableHashSet<string>? tags)
    {
        if (tags is not { IsEmpty: false } || target.Value is null)
        {
            return target;
        }
        return target.WithValue(UnionResolvedValue(target.Value, tags));
    }

    /// <summary>
    /// A resolved environment value. Terminal values have already completed their logical expression evaluation and must not be evaluated again.
    /// </summary>
    internal protected readonly record struct Evaluation(object? Value, bool Terminal)
    {
        public static Evaluation Of(object? value) => new(value, Terminal: false);

        public static Evaluation TerminalValue(object? value) => new(value, Terminal: true);

        public Evaluation WithValue(object? value) => new(value, Terminal);
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
