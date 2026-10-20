using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Engine.Expressions.Bound;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Text;
using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Cyborg.Core.Runtime.Engine.Environments;

public partial record RuntimeEnvironment(string Name, bool IsTransient, VariableSyntaxBuilder SyntaxFactory, string Namespace)
    : EnvironmentLike(SyntaxFactory, Namespace), IRuntimeEnvironment, ITransactionalRuntimeEnvironment
{
    internal RuntimeEnvironmentId EnvironmentId { get; init; } = RuntimeEnvironmentId.Create();

    public IReadOnlyCollection<string> OverrideResolutionTags { get; init; } = [];

    RuntimeEnvironmentId ITransactionalRuntimeEnvironment.EnvironmentId => EnvironmentId;

    IReadOnlyCollection<T>? IRuntimeEnvironment.ResolveCollection<TModule, T>(
        TModule module,
        IReadOnlyCollection<T>? value,
        string moduleExpression,
        string valueExpression) =>
        TryResolveCollectionCore(this, module, value, moduleExpression, valueExpression, out IReadOnlyCollection<T>? selected) ? selected : value;

    /// <summary>
    /// Selects collection overrides in structural view order, retaining the original resolution entry point.
    /// Each candidate is resolved recursively, so a candidate may also be found in an inherited store.
    /// </summary>
    internal protected virtual bool TryResolveCollectionCore<TModule, T>(
        EnvironmentLike entryPoint,
        TModule module,
        IReadOnlyCollection<T>? value,
        string? moduleExpression,
        string? valueExpression,
        out IReadOnlyCollection<T>? resolvedValue)
        where TModule : ModuleBase, IModuleDefinition
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(module);
        string valuePath = ConstructValueResolutionPath(value, moduleExpression, valueExpression);

        RuntimeEnvironment lastScope = this;
        foreach (RuntimeEnvironment scope in EnumerateOverrideScopes())
        {
            lastScope = scope;
            foreach (string identifier in scope.EnumerateOverrideIdentifiers(module.Name, module.Group, TModule.ModuleId))
            {
                string overridePath = scope.SyntaxFactory.Path(identifier, valuePath).Override();
                if (!scope.TryResolveEvaluation(overridePath, entryPoint, out Evaluation evaluation))
                {
                    continue;
                }
                if (evaluation.Value is null)
                {
                    resolvedValue = null;
                    return true;
                }
                if (evaluation.Value is not IEnumerable enumerable)
                {
                    throw new InvalidCastException(
                        $"Attempted to resolve variable '{overridePath}' as type {typeof(IEnumerable).FullName}, but it is of type {evaluation.Value.GetType().FullName}.");
                }
                resolvedValue = enumerable is IReadOnlyCollection<T> typedCollection ? typedCollection : enumerable.Cast<T>().ToImmutableArray();
                return true;
            }
        }

        // A non-RuntimeEnvironment parent cannot participate in the internal override traversal.
        // Preserve its public resolution boundary, just as the inherited implementation did.
        if (lastScope is InheritedRuntimeEnvironment { Parent: not RuntimeEnvironment } inherited)
        {
            resolvedValue = inherited.Parent.Resolve(module, value, moduleExpression, valueExpression);
            return true;
        }

        resolvedValue = default;
        return false;
    }

    public virtual T? Resolve<TModule, T>(TModule module, T? value, [CallerArgumentExpression(nameof(module))] string? moduleExpression = null, [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
        where TModule : ModuleBase, IModuleDefinition
    {
        T? resolvedValue = TryResolveCore(this, module, value, moduleExpression, valueExpression, out T? selected, out bool terminal) ? selected : value;
        if (terminal)
        {
            return resolvedValue;
        }
        if (resolvedValue is string stringValue)
        {
            TaggedString interpolated = InterpolateCore(stringValue, entryPoint: this);
            return typeof(T) == typeof(string) ? (T)(object)interpolated.Value : (T)(object)interpolated;
        }
        if (resolvedValue is TaggedString tagged)
        {
            TaggedString interpolated = InterpolateCore(tagged, entryPoint: this);
            return typeof(T) == typeof(string) ? (T)(object)interpolated.Value : (T)(object)interpolated;
        }
        return resolvedValue;
    }

    /// <summary>
    /// Selects typed overrides in structural view order. Terminal snapshots bypass the caller's final interpolation pass.
    /// A candidate can resolve through inherited stores; view-local identifier order is preserved.
    /// </summary>
    internal protected virtual bool TryResolveCore<TModule, T>(
        EnvironmentLike entryPoint,
        TModule module,
        T? value,
        string? moduleExpression,
        string? valueExpression,
        out T? resolvedValue,
        out bool terminal) where TModule : ModuleBase, IModuleDefinition
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(module);
        terminal = false;
        string valuePath = ConstructValueResolutionPath(value, moduleExpression, valueExpression);

        RuntimeEnvironment lastScope = this;
        foreach (RuntimeEnvironment scope in EnumerateOverrideScopes())
        {
            lastScope = scope;
            foreach (string identifier in scope.EnumerateOverrideIdentifiers(module.Name, module.Group, TModule.ModuleId))
            {
                string overridePath = scope.SyntaxFactory.Path(identifier, valuePath).Override();
                if (!scope.TryResolveEvaluation(overridePath, entryPoint, out Evaluation evaluation))
                {
                    continue;
                }
                if (!scope.TryConvertResolvedValue(evaluation.Value, overridePath, notifyImplicitConversion: true, out resolvedValue))
                {
                    throw new InvalidCastException($"Attempted to resolve variable '{overridePath}' as type {typeof(T).FullName}, but it is of type {evaluation.Value?.GetType().FullName ?? "null"}.");
                }
                terminal = evaluation.Terminal;
                return true;
            }
        }

        if (lastScope is InheritedRuntimeEnvironment { Parent: not RuntimeEnvironment } inherited)
        {
            terminal = true;
            resolvedValue = inherited.Parent.Resolve(module, value, moduleExpression, valueExpression);
            return true;
        }

        resolvedValue = default;
        return false;
    }

    /// <summary>Retrieves a selected textual override and its actual storage view without evaluating or reinterpreting it.</summary>
    internal bool TrySelectBoundOverride<TModule>(
        TModule module,
        string moduleExpression,
        string valueExpression,
        [NotNullWhen(true)] out SelectedBoundOverride? selected)
        where TModule : ModuleBase, IModuleDefinition
    {
        ArgumentNullException.ThrowIfNull(module);
        string valuePath = ConstructValueResolutionPath<object?>(value: null, moduleExpression, valueExpression);
        foreach (RuntimeEnvironment scope in EnumerateOverrideScopes())
        {
            foreach (string identifier in scope.EnumerateOverrideIdentifiers(module.Name, module.Group, TModule.ModuleId))
            {
                string overridePath = scope.SyntaxFactory.Path(identifier, valuePath).Override();
                if (scope.TryGetStoredVariableRecursiveCore(overridePath, out object? stored, out EnvironmentLike? source) && stored is BoundValue expression)
                {
                    selected = new SelectedBoundOverride(expression, source);
                    return true;
                }
            }
        }
        selected = null;
        return false;
    }

    /// <summary>
    /// Enumerates contextual views of one structural parent chain, starting with the child.
    /// This is unrelated to runtime invocation ancestry or transactional graph traversal.
    /// </summary>
    private IEnumerable<RuntimeEnvironment> EnumerateOverrideScopes()
    {
        RuntimeEnvironment current = this;
        while (true)
        {
            yield return current;
            if (current is not InheritedRuntimeEnvironment { Parent: RuntimeEnvironment parent })
            {
                yield break;
            }
            current = parent;
        }
    }

    private string ConstructValueResolutionPath<T>(T? value, string? moduleExpression, string? valueExpression)
    {
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
        string valuePath = NamingPolicy.ConvertName(valueSpan.Slice(1, valueSpan.Length - skippedChars).ToString());
        return valuePath;
    }

    private IEnumerable<string> EnumerateOverrideIdentifiers(string? name, string? group, string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (SyntaxFactory.IsValidIdentifier(name))
        {
            yield return name;
        }
        if (SyntaxFactory.IsValidIdentifier(group))
        {
            yield return group;
        }
        yield return moduleId;
        foreach (string tag in OverrideResolutionTags)
        {
            yield return tag;
        }
    }

    void IRuntimeEnvironment.Publish<TModule, T>(TModule module, string root, T decomposable)
    {
        ArgumentNullException.ThrowIfNull(module);
        Publish(root, decomposable, module.Artifacts.DecompositionStrategy, module.Artifacts.PublishNullValues);
    }

    public IRuntimeEnvironment Bind(string ns)
    {
        ArgumentNullException.ThrowIfNull(ns);
        return this with
        {
            Namespace = ns
        };
    }

    IRuntimeEnvironment ITransactionalRuntimeEnvironment.BindTransaction(
        RuntimeEnvironmentTransactionParticipant participant,
        ActiveTransaction activeTransaction) =>
        BindTransactionCore(participant, activeTransaction);

    private protected virtual IRuntimeEnvironment BindTransactionCore(
        RuntimeEnvironmentTransactionParticipant participant,
        ActiveTransaction activeTransaction)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(activeTransaction);
        return this with
        {
            VariableStore = new TransactionalEnvironmentVariableStore(EnvironmentId, participant, activeTransaction)
        };
    }

    internal RuntimeEnvironment(
        RuntimeEnvironmentId environmentId,
        RuntimeEnvironmentNode node,
        VariableSyntaxBuilder syntaxFactory,
        string ns,
        RuntimeEnvironmentTransactionParticipant participant,
        ActiveTransaction activeTransaction)
        : this(node.Name, node.IsTransient, syntaxFactory, ns)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(syntaxFactory);
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(activeTransaction);
        EnvironmentId = environmentId;
        VariableStore = new TransactionalEnvironmentVariableStore(environmentId, participant, activeTransaction);
    }

    public IRuntimeEnvironment WithOverrideResolutionTags(IReadOnlyCollection<string> tags) => this with
    {
        OverrideResolutionTags = tags
    };
}
