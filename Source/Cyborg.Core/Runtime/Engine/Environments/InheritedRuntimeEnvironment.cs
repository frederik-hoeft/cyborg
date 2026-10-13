using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;

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
}
