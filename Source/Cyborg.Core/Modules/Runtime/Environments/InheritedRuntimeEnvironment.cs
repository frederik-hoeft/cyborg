using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using Cyborg.Core.Text;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal sealed record InheritedRuntimeEnvironment(string Name, IRuntimeEnvironment Parent, bool IsTransient, VariableSyntaxBuilder SyntaxFactory, string Namespace)
    : RuntimeEnvironment(Name, IsTransient, SyntaxFactory, Namespace)
{
    private protected override IRuntimeEnvironment BindTransactionCore(
        RuntimeEnvironmentTransactionParticipant participant,
        ExecutionTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(transaction);
        return this with
        {
            Parent = Parent is ITransactionalRuntimeEnvironment transactionalParent
                ? transactionalParent.BindTransaction(participant, transaction)
                : throw new InvalidOperationException($"Runtime environment type '{Parent.GetType().FullName}' does not expose transactional environment identity."),
            VariableStore = new TransactionalEnvironmentVariableStore(EnvironmentId, participant, transaction)
        };
    }

    internal InheritedRuntimeEnvironment(
        RuntimeEnvironmentId environmentId,
        RuntimeEnvironmentNode node,
        IRuntimeEnvironment parent,
        VariableSyntaxBuilder syntaxFactory,
        string ns,
        RuntimeEnvironmentTransactionParticipant participant,
        ExecutionTransaction transaction)
        : this(node.Name, parent, node.IsTransient, syntaxFactory, ns)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(syntaxFactory);
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(transaction);
        EnvironmentId = environmentId;
        VariableStore = new TransactionalEnvironmentVariableStore(environmentId, participant, transaction);
    }

    internal protected override bool TryGetStoredVariableRecursiveCore(string name, [NotNullWhen(true)] out object? value)
    {
        if (TryGetStoredVariableInCurrentScopeCore(name, out value))
        {
            return true;
        }
        if (Parent is EnvironmentLike parent)
        {
            return parent.TryGetStoredVariableRecursiveCore(name, out value);
        }
        value = default;
        return false;
    }

    internal protected override bool TryResolveVariableRecursiveCore(ResolutionContext context, [NotNullWhen(true)] out object? value)
    {
        if (TryResolveVariableInCurrentScopeCore(context, out value))
        {
            return true;
        }
        if (Parent is EnvironmentLike parent)
        {
            return parent.TryResolveVariableRecursiveCore(context, out value);
        }
        return Parent.TryResolveVariable(context.Name, out value);
    }

    internal protected override bool TrySelectRawStringOverrideCore<TModule>(EnvironmentLike entryPoint, TModule module, string? moduleExpression, string? valueExpression, [NotNullWhen(true)] out string? value)
    {
        if (base.TrySelectRawStringOverrideCore(entryPoint, module, moduleExpression, valueExpression, out value))
        {
            return true;
        }
        if (Parent is RuntimeEnvironment runtimeParent)
        {
            return runtimeParent.TrySelectRawStringOverrideCore(entryPoint, module, moduleExpression, valueExpression, out value);
        }
        value = default;
        return false;
    }

    internal protected override bool TrySelectRawTaggedStringOverrideCore<TModule>(EnvironmentLike entryPoint, TModule module, string? moduleExpression, string? valueExpression, out TaggedString value)
    {
        if (base.TrySelectRawTaggedStringOverrideCore(entryPoint, module, moduleExpression, valueExpression, out value))
        {
            return true;
        }
        if (Parent is RuntimeEnvironment runtimeParent)
        {
            return runtimeParent.TrySelectRawTaggedStringOverrideCore(entryPoint, module, moduleExpression, valueExpression, out value);
        }
        value = default;
        return false;
    }

    [return: NotNullIfNotNull(nameof(value))]
    internal protected override IReadOnlyCollection<T>? ResolveCollectionCore<TModule, T>(EnvironmentLike entryPoint, TModule module, IReadOnlyCollection<T>? value, string? moduleExpression, string? valueExpression)
    {
        IReadOnlyCollection<T>? resolvedValue = base.ResolveCollectionCore(entryPoint, module, value, moduleExpression, valueExpression);
        if (resolvedValue is not null && !resolvedValue.Equals(value))
        {
            return resolvedValue;
        }
        if (Parent is RuntimeEnvironment runtimeParent)
        {
            return runtimeParent.ResolveCollectionCore(entryPoint, module, value, moduleExpression, valueExpression);
        }
        return Parent.Resolve(module, value, moduleExpression, valueExpression);
    }

    [return: NotNullIfNotNull(nameof(value))]
    internal protected override T? ResolveCore<TModule, T>(EnvironmentLike entryPoint, TModule module, T? value, string? moduleExpression, string? valueExpression) where T : default
    {
        T? resolvedValue = base.ResolveCore(entryPoint, module, value, moduleExpression, valueExpression);
        if (resolvedValue is not null && !resolvedValue.Equals(value))
        {
            return resolvedValue;
        }
        if (Parent is RuntimeEnvironment runtimeParent)
        {
            return runtimeParent.ResolveCore(entryPoint, module, value, moduleExpression, valueExpression);
        }
        return Parent.Resolve(module, value, moduleExpression, valueExpression);
    }
}
