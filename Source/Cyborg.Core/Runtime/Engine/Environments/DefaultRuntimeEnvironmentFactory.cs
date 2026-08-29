using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Text;

namespace Cyborg.Core.Runtime.Engine.Environments;

internal sealed class DefaultRuntimeEnvironmentFactory(
    VariableSyntaxBuilder syntaxFactory,
    ITaggedStringConversionObserver? taggedStringConversionObserver) : IRuntimeEnvironmentFactory
{
    public GlobalRuntimeEnvironment CreateGlobalEnvironment() => AttachRuntimeServices(new GlobalRuntimeEnvironment(syntaxFactory));

    public IEnvironmentLike CreateEnvironmentLike(string ns)
    {
        ArgumentNullException.ThrowIfNull(ns);
        return AttachRuntimeServices(new EnvironmentLike(syntaxFactory, ns));
    }

    public IRuntimeEnvironment BindTransaction(
        IRuntimeEnvironment environment,
        RuntimeEnvironmentTransactionParticipant participant,
        ModuleTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(transaction);
        if (environment is not ITransactionalRuntimeEnvironment transactionalEnvironment)
        {
            throw new InvalidOperationException($"Runtime environment type '{environment.GetType().FullName}' does not expose transactional environment identity.");
        }
        IRuntimeEnvironment boundEnvironment = transactionalEnvironment.BindTransaction(participant, transaction);
        if (boundEnvironment is not RuntimeEnvironment runtimeEnvironment)
        {
            throw new InvalidOperationException($"Runtime environment type '{boundEnvironment.GetType().FullName}' cannot receive Cyborg runtime services.");
        }
        return AttachRuntimeServices(runtimeEnvironment);
    }

    public IRuntimeEnvironment CreateTransactionView(
        RuntimeEnvironmentId environmentId,
        RuntimeEnvironmentNode node,
        IRuntimeEnvironment? parent,
        string ns,
        RuntimeEnvironmentTransactionParticipant participant,
        ModuleTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(ns);
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(transaction);

        RuntimeEnvironment environment = parent is null
            ? new RuntimeEnvironment(environmentId, node, syntaxFactory, ns, participant, transaction)
            : new InheritedRuntimeEnvironment(environmentId, node, parent, syntaxFactory, ns, participant, transaction);
        return AttachRuntimeServices(environment);
    }

    private EnvironmentLike AttachRuntimeServices(EnvironmentLike environment) => environment with
    {
        TaggedStringConversionObserver = taggedStringConversionObserver
    };

    private RuntimeEnvironment AttachRuntimeServices(RuntimeEnvironment environment) => environment with
    {
        TaggedStringConversionObserver = taggedStringConversionObserver
    };

    private GlobalRuntimeEnvironment AttachRuntimeServices(GlobalRuntimeEnvironment environment) => environment with
    {
        TaggedStringConversionObserver = taggedStringConversionObserver
    };
}
