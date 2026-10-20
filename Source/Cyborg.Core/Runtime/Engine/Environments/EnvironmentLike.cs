using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Runtime.Engine.Environments.Artifacts;
using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Engine.Expressions;
using Cyborg.Core.Runtime.Engine.Expressions.Bound;
using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using Cyborg.Core.Runtime.Engine.Expressions.Grammars;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using Cyborg.Core.Text;
using System.Collections;
using System.Text.Json;

namespace Cyborg.Core.Runtime.Engine.Environments;

public partial record EnvironmentLike(VariableSyntaxBuilder SyntaxFactory, string Namespace) : IEnvironmentLike
{
    private protected IEnvironmentVariableStore VariableStore { get; init; } = new MutableEnvironmentVariableStore();

    private static readonly ExpressionMaterializer s_materializer = new();

    internal ITaggedStringConversionObserver? TaggedStringConversionObserver { get; init; }

    internal EnvironmentContext Context { get; init; } = EnvironmentContext.Empty;

    internal ContextualVariableRegistry ContextualVariables { get; init; } = ContextualVariableRegistry.Default;

    protected JsonNamingPolicy NamingPolicy => SyntaxFactory.NamingPolicy;

    internal protected virtual bool TryResolveVariableInCurrentScopeCore(ResolutionContext context, out Evaluation evaluation)
    {
        if (TryResolveStructuralInCurrentScopeCore(context, out EvaluatedValue? result))
        {
            evaluation = Evaluation.TerminalValue(s_materializer.Materialize(result));
            return true;
        }
        evaluation = default;
        return false;
    }

    internal virtual bool TryResolveStructuralInCurrentScopeCore(ResolutionContext context, [NotNullWhen(true)] out EvaluatedValue? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ContextualVariableRegistry.IsContextualName(context.Name))
        {
            bool resolved = ContextualVariables.TryResolve(context.Name, Context, Namespace, out object? contextualValue);
            value = resolved ? new EvaluatedTerminal(contextualValue) : null;
            return resolved;
        }
        if (VariableStore.TryGetValue(context.Name, out object? stored) && stored is BoundValue bound)
        {
            value = bound.Evaluate(new EnvironmentExpressionContext(this, context));
            return true;
        }
        value = null;
        return false;
    }

    internal virtual bool TryResolveStructuralRecursiveCore(ResolutionContext context, [NotNullWhen(true)] out EvaluatedValue? value) =>
        TryResolveStructuralInCurrentScopeCore(context, out value);

    private sealed class EnvironmentExpressionContext(EnvironmentLike scope, ResolutionContext context) : IExpressionResolutionContext
    {
        public bool TryResolve(VariableReferenceSyntax reference, [NotNullWhen(true)] out EvaluatedValue? value)
        {
            string name = reference.Identifier ?? scope.SyntaxFactory.Self();
            ResolutionOrigin origin = reference.Anchor == ReferenceAnchor.EntryPoint ? ResolutionOrigin.EntryPoint : ResolutionOrigin.CurrentScope;
            ResolutionContext next = context.With(name, origin);
            EnvironmentLike target = origin == ResolutionOrigin.EntryPoint ? context.EntryPoint : scope;
            return target.TryResolveStructuralRecursiveCore(next, out value);
        }
    }

    protected virtual bool TryGetStoredVariableInCurrentScopeCore(string name, out object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (VariableStore.TryGetValue(name, out value))
        {
            return true;
        }
        value = default;
        return false;
    }

    internal protected virtual bool TryGetStoredVariableRecursiveCore(string name, out object? value) =>
        TryGetStoredVariableRecursiveCore(name, out value, out _);

    /// <summary>Returns the stored value and the actual structural view that owns it.</summary>
    internal protected virtual bool TryGetStoredVariableRecursiveCore(string name, out object? value, [NotNullWhen(true)] out EnvironmentLike? source)
    {
        if (TryGetStoredVariableInCurrentScopeCore(name, out value))
        {
            source = this;
            return true;
        }
        source = null;
        return false;
    }

    internal protected virtual bool TryResolveVariableRecursiveCore(ResolutionContext context, out Evaluation evaluation) =>
        TryResolveVariableInCurrentScopeCore(context, out evaluation);

    public virtual bool TryResolveVariable<T>(string name, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!TryResolveEvaluation(name, entryPoint: this, out Evaluation evaluation))
        {
            value = default;
            return false;
        }
        object? resolved = evaluation.Value;
        if (TryConvertResolvedValue(resolved, name, notifyImplicitConversion: true, out value))
        {
            return true;
        }
        throw new InvalidCastException($"Attempted to resolve variable '{name}' as type {typeof(T).FullName}, but it is of type {resolved?.GetType().FullName ?? "null"}.");
    }

    public virtual void SetVariable<T>(string name, T value)
    {
        EnsureWritableName(name);
        BoundValue bound = ValueGrammar.Instance.Compile(value).Bind(new EnvironmentExpressionContext(this, ResolutionContext.CreateRoot(this)));
        VariableStore.SetValue(name, bound);
    }

    /// <summary>Reads terminal structure without crossing the public CLR materialization boundary.</summary>
    internal bool TryResolveStructuralValue(string name, [NotNullWhen(true)] out EvaluatedValue? value) =>
        TryResolveStructuralRecursiveCore(ResolutionContext.Create(this, name), out value);

    /// <summary>Compiles a new effective value against this runtime view, retaining its structural representation.</summary>
    internal BoundValue BindStructuralValue(object value) =>
        ValueGrammar.Instance.Compile(value).Bind(new EnvironmentExpressionContext(this, ResolutionContext.CreateRoot(this)));

    /// <summary>Binds text without performing whole-value typed interpretation, for explicitly deferred interpolation.</summary>
    internal BoundValue BindStructuralText(string text) =>
        ValueGrammar.Instance.ParseText(text).Bind(new EnvironmentExpressionContext(this, ResolutionContext.CreateRoot(this)));

    /// <summary>Evaluates a bound value in this view while preserving an independent original resolution entry point.</summary>
    internal EvaluatedValue EvaluateStructuralValue(BoundValue bound, EnvironmentLike entryPoint)
    {
        ArgumentNullException.ThrowIfNull(bound);
        ArgumentNullException.ThrowIfNull(entryPoint);
        return bound.Evaluate(new EnvironmentExpressionContext(this, ResolutionContext.CreateRoot(entryPoint)));
    }

    /// <summary>Publishes an already evaluated value as a terminal binding, retaining captured expression text as literal data.</summary>
    internal void SetTerminalVariable(string name, EvaluatedValue terminal)
    {
        EnsureWritableName(name);
        ArgumentNullException.ThrowIfNull(terminal);
        VariableStore.SetValue(name, new BoundCapture(terminal));
    }

    public virtual bool TryRemoveVariable(string name)
    {
        EnsureWritableName(name);
        return VariableStore.TryRemove(name);
    }

    private static void EnsureWritableName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (ContextualVariableRegistry.IsContextualName(name))
        {
            throw new ArgumentException($"Contextual variable '{name}' is runtime-owned and cannot be modified.", nameof(name));
        }
    }

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
            yield return new KeyValuePair<string, object?>(key, value is BoundValue bound ? bound.PresentStoredValue() : value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Retains structural bindings when seeding or importing transactional environment state.</summary>
    internal IReadOnlyCollection<KeyValuePair<string, object?>> SnapshotStoredBindings() => [.. VariableStore];

    /// <summary>
    /// Copies immutable bound values directly without using public CLR presentation or reparsing.
    /// Captures remain terminal and lazy expressions retain their structural references.
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
        ValueSyntax syntax = ValueGrammar.Instance.ParseText(template.Value);
        BoundValue bound = new TaggedValueSyntax(syntax, template.Tags).Bind(new EnvironmentExpressionContext(this, ResolutionContext.CreateRoot(entryPoint)));
        object? result = s_materializer.Materialize(bound.Evaluate(new EnvironmentExpressionContext(this, ResolutionContext.CreateRoot(entryPoint))));
        return result is TaggedString tagged ? tagged : new TaggedString((string)result!);
    }

    private protected bool TryConvertResolvedValue<T>(object? objValue, string variableName, bool notifyImplicitConversion, out T? value)
    {
        if (objValue is null && default(T) is null)
        {
            value = default;
            return true;
        }
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
