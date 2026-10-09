using Cyborg.Core.Runtime.Engine.Environments.Syntax;
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

    [return: NotNullIfNotNull(nameof(value))]
    IReadOnlyCollection<T>? IRuntimeEnvironment.ResolveCollection<TModule, T>(
        TModule module,
        IReadOnlyCollection<T>? value,
        string moduleExpression,
        string valueExpression) =>
        TryResolveCollectionCore(this, module, value, moduleExpression, valueExpression, out IReadOnlyCollection<T>? selected) ? selected : value;

    string IRuntimeEnvironment.ResolveValueExpression(string value, bool willInterpolate) =>
        ResolveValueExpressionCore(value, willInterpolate);

    TaggedString IRuntimeEnvironment.ResolveValueExpression(TaggedString value, bool willInterpolate) =>
        ResolveValueExpressionCore(value, willInterpolate);

    TaggedString? IRuntimeEnvironment.ResolveValueExpression(TaggedString? value, bool willInterpolate) =>
        value is { } tagged ? ResolveValueExpressionCore(tagged, willInterpolate) : null;

    /// <summary>
    /// Reports a selected override (or completed parent resolution), independently of whether its value equals the configured collection.
    /// </summary>
    internal protected virtual bool TryResolveCollectionCore<TModule, T>(
        EnvironmentLike entryPoint,
        TModule module,
        IReadOnlyCollection<T>? value,
        string? moduleExpression,
        string? valueExpression,
        [NotNullWhen(true)] out IReadOnlyCollection<T>? resolvedValue)
        where TModule : ModuleBase, IModuleDefinition
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(module);
        string valuePath = ConstructValueResolutionPath(value, moduleExpression, valueExpression);

        foreach (string identifier in EnumerateOverrideIdentifiers(module.Name, module.Group, TModule.ModuleId))
        {
            string overridePath = SyntaxFactory.Path(identifier, valuePath).Override();
            if (!TryResolveEvaluation(overridePath, entryPoint, out Evaluation evaluation) || evaluation.Value is null)
            {
                continue;
            }
            if (evaluation.Value is not IEnumerable enumerable)
            {
                throw new InvalidCastException($"Attempted to resolve variable '{overridePath}' as type {typeof(IEnumerable).FullName}, but it is of type {evaluation.Value.GetType().FullName}.");
            }
            resolvedValue = enumerable is IReadOnlyCollection<T> typedCollection ? typedCollection : enumerable.Cast<T>().ToImmutableArray();
            return true;
        }

        resolvedValue = default;
        return false;
    }

    [return: NotNullIfNotNull(nameof(value))]
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
    /// Reports a selected override (or completed parent resolution). The terminal flag controls text evaluation, not override precedence.
    /// </summary>
    internal protected virtual bool TryResolveCore<TModule, T>(
        EnvironmentLike entryPoint,
        TModule module,
        T? value,
        string? moduleExpression,
        string? valueExpression,
        [NotNullWhen(true)] out T? resolvedValue,
        out bool terminal) where TModule : ModuleBase, IModuleDefinition
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(module);
        terminal = false;
        string valuePath = ConstructValueResolutionPath(value, moduleExpression, valueExpression);

        foreach (string identifier in EnumerateOverrideIdentifiers(module.Name, module.Group, TModule.ModuleId))
        {
            string overridePath = SyntaxFactory.Path(identifier, valuePath).Override();
            if (!TryResolveEvaluation(overridePath, entryPoint, out Evaluation evaluation) || evaluation.Value is null)
            {
                continue;
            }
            if (!TryConvertResolvedValue(evaluation.Value, overridePath, notifyImplicitConversion: true, out resolvedValue))
            {
                throw new InvalidCastException($"Attempted to resolve variable '{overridePath}' as type {typeof(T).FullName}, but it is of type {evaluation.Value.GetType().FullName}.");
            }
            terminal = evaluation.Terminal;
            return true;
        }

        resolvedValue = default;
        return false;
    }

    [return: NotNullIfNotNull(nameof(value))]
    string? IRuntimeEnvironment.SelectRawStringOverride<TModule>(TModule module, string? value, string moduleExpression, string valueExpression, bool shieldInterpolation) =>
        TrySelectRawStringOverrideCore(this, module, moduleExpression, valueExpression, shieldInterpolation, out string? selectedValue) ? selectedValue : value;

    TaggedString IRuntimeEnvironment.SelectRawTaggedStringOverride<TModule>(TModule module, TaggedString value, string moduleExpression, string valueExpression, bool shieldInterpolation) =>
        TrySelectRawTaggedStringOverrideCore(this, module, moduleExpression, valueExpression, shieldInterpolation, out TaggedString selectedValue) ? selectedValue : value;

    [return: NotNullIfNotNull(nameof(value))]
    TaggedString? IRuntimeEnvironment.SelectRawTaggedStringOverride<TModule>(TModule module, TaggedString? value, string moduleExpression, string valueExpression, bool shieldInterpolation) =>
        TrySelectRawTaggedStringOverrideCore(this, module, moduleExpression, valueExpression, shieldInterpolation, out TaggedString selectedValue) ? selectedValue : value;

    internal protected virtual bool TrySelectRawTaggedStringOverrideCore<TModule>(
        EnvironmentLike entryPoint,
        TModule module,
        string? moduleExpression,
        string? valueExpression,
        bool shieldInterpolation,
        out TaggedString value)
        where TModule : ModuleBase, IModuleDefinition
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(module);
        string valuePath = ConstructValueResolutionPath<TaggedString>(value: default, moduleExpression, valueExpression);

        foreach (string identifier in EnumerateOverrideIdentifiers(module.Name, module.Group, TModule.ModuleId))
        {
            string overridePath = SyntaxFactory.Path(identifier, valuePath).Override();
            if (TryGetStoredVariable(overridePath, shieldInterpolation, out TaggedString selectedValue))
            {
                value = selectedValue;
                return true;
            }
        }

        value = default;
        return false;
    }

    internal protected virtual bool TrySelectRawStringOverrideCore<TModule>(
        EnvironmentLike entryPoint,
        TModule module,
        string? moduleExpression,
        string? valueExpression,
        bool shieldInterpolation,
        [NotNullWhen(true)] out string? value)
        where TModule : ModuleBase, IModuleDefinition
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(module);
        string valuePath = ConstructValueResolutionPath<string>(value: null, moduleExpression, valueExpression);

        foreach (string identifier in EnumerateOverrideIdentifiers(module.Name, module.Group, TModule.ModuleId))
        {
            string overridePath = SyntaxFactory.Path(identifier, valuePath).Override();
            if (TryGetStoredVariable(overridePath, shieldInterpolation, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
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
