using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Text;

namespace Cyborg.Core.Runtime.Engine.Environments;

internal sealed record InheritedRuntimeEnvironment(string Name, IRuntimeEnvironment Parent, bool IsTransient, VariableSyntaxBuilder SyntaxFactory, string Namespace)
    : RuntimeEnvironment(Name, IsTransient, SyntaxFactory, Namespace)
{
    private protected override IRuntimeEnvironment BindTransactionCore(
        RuntimeEnvironmentTransactionParticipant participant,
        ActiveTransaction activeTransaction)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(activeTransaction);
        return this with
        {
            Parent = Parent is ITransactionalRuntimeEnvironment transactionalParent
                ? transactionalParent.BindTransaction(participant, activeTransaction)
                : throw new InvalidOperationException($"Runtime environment type '{Parent.GetType().FullName}' does not expose transactional environment identity."),
            VariableStore = new TransactionalEnvironmentVariableStore(EnvironmentId, participant, activeTransaction)
        };
    }

    internal InheritedRuntimeEnvironment(
        RuntimeEnvironmentId environmentId,
        RuntimeEnvironmentNode node,
        IRuntimeEnvironment parent,
        VariableSyntaxBuilder syntaxFactory,
        string ns,
        RuntimeEnvironmentTransactionParticipant participant,
        ActiveTransaction activeTransaction)
        : this(node.Name, parent, node.IsTransient, syntaxFactory, ns)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(syntaxFactory);
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(activeTransaction);
        EnvironmentId = environmentId;
        VariableStore = new TransactionalEnvironmentVariableStore(environmentId, participant, activeTransaction);
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

    internal protected override bool TryResolveVariableRecursiveCore(ResolutionContext context, out Evaluation evaluation)
    {
        if (TryResolveVariableInCurrentScopeCore(context, out evaluation))
        {
            return true;
        }
        if (Parent is EnvironmentLike parent)
        {
            return parent.TryResolveVariableRecursiveCore(context, out evaluation);
        }
        if (Parent.TryResolveVariable(context.Name, out object? value))
        {
            // The public parent read already finalized textual escapes.
            evaluation = Evaluation.TerminalValue(value);
            return true;
        }
        evaluation = default;
        return false;
    }

    internal protected override bool TrySelectRawStringOverrideCore<TModule>(
        EnvironmentLike entryPoint,
        TModule module,
        string? moduleExpression,
        string? valueExpression,
        bool shieldInterpolation,
        [NotNullWhen(true)] out string? value)
    {
        if (base.TrySelectRawStringOverrideCore(entryPoint, module, moduleExpression, valueExpression, shieldInterpolation, out value))
        {
            return true;
        }
        if (Parent is RuntimeEnvironment runtimeParent)
        {
            return runtimeParent.TrySelectRawStringOverrideCore(entryPoint, module, moduleExpression, valueExpression, shieldInterpolation, out value);
        }
        value = default;
        return false;
    }

    internal protected override bool TrySelectRawTaggedStringOverrideCore<TModule>(
        EnvironmentLike entryPoint,
        TModule module,
        string? moduleExpression,
        string? valueExpression,
        bool shieldInterpolation,
        out TaggedString value)
    {
        if (base.TrySelectRawTaggedStringOverrideCore(entryPoint, module, moduleExpression, valueExpression, shieldInterpolation, out value))
        {
            return true;
        }
        if (Parent is RuntimeEnvironment runtimeParent)
        {
            return runtimeParent.TrySelectRawTaggedStringOverrideCore(entryPoint, module, moduleExpression, valueExpression, shieldInterpolation, out value);
        }
        value = default;
        return false;
    }

    [return: NotNullIfNotNull(nameof(value))]
    internal protected override IReadOnlyCollection<T>? ResolveCollectionCore<TModule, T>(
        EnvironmentLike entryPoint,
        TModule module,
        IReadOnlyCollection<T>? value,
        string? moduleExpression,
        string? valueExpression)
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
    internal protected override T? ResolveCore<TModule, T>(
        EnvironmentLike entryPoint,
        TModule module,
        T? value,
        string? moduleExpression,
        string? valueExpression,
        out bool terminal) where T : default
    {
        T? resolvedValue = base.ResolveCore(entryPoint, module, value, moduleExpression, valueExpression, out terminal);
        if (terminal || (resolvedValue is not null && !resolvedValue.Equals(value)))
        {
            return resolvedValue;
        }
        if (Parent is RuntimeEnvironment runtimeParent)
        {
            return runtimeParent.ResolveCore(entryPoint, module, value, moduleExpression, valueExpression, out terminal);
        }
        terminal = true;
        return Parent.Resolve(module, value, moduleExpression, valueExpression);
    }
}
