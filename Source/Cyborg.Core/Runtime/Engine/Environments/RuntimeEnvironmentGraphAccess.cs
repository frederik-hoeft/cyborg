using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine.Environments;

/// <summary>
/// Accesses logical environment topology and registrations in a specified transaction.
/// View bindings and invocation ancestry are not part of the graph.
/// </summary>
internal sealed class RuntimeEnvironmentGraphAccess(RuntimeEnvironmentTransactionParticipant participant)
{
    public RuntimeEnvironmentTransactionParticipant Participant => participant;

    public void ValidateGlobalEnvironment(GlobalRuntimeEnvironment globalEnvironment, ActiveTransaction active)
    {
        RuntimeEnvironmentTransactionState state = GetState(active);
        if (state.GlobalEnvironmentId != GetEnvironmentId(globalEnvironment))
        {
            throw new InvalidOperationException("The runtime environment transaction seed does not match the supplied logical global environment.");
        }
    }

    public RuntimeEnvironmentNode GetNode(RuntimeEnvironmentId environmentId, RuntimeEnvironmentTransactionState state)
    {
        if (!state.TryGetEnvironment(environmentId, out RuntimeEnvironmentNode? node))
        {
            throw new InvalidOperationException($"Runtime environment '{environmentId}' does not exist in the current transaction.");
        }
        return node;
    }

    public bool TryGetNamedEnvironmentId(string name, ActiveTransaction active, out RuntimeEnvironmentId environmentId) =>
        GetState(active).TryGetRegisteredEnvironment(name, out environmentId);

    public RuntimeEnvironmentId CreateNode(string name, bool transient, RuntimeEnvironmentParent? parent, ActiveTransaction active)
    {
        RuntimeEnvironmentId environmentId = RuntimeEnvironmentId.Create();
        RuntimeEnvironmentNode node = new(name, transient, parent);
        RuntimeEnvironmentTransactionState state = GetState(active);
        if (transient)
        {
            state.AddEnvironment(environmentId, node, values: []);
        }
        else if (!state.TryAddNamedEnvironment(environmentId, node, values: []))
        {
            throw new InvalidOperationException($"Attempting to create a named environment that already exists: {name}");
        }
        return environmentId;
    }

    /// <summary>
    /// Imports an externally supplied view into the selected transaction, retaining its structural parent chain.
    /// An existing logical node is never rewritten from a differently bound view.
    /// </summary>
    public void EnsureEnvironmentExists(IRuntimeEnvironment environment, ActiveTransaction active)
    {
        RuntimeEnvironmentId environmentId = GetEnvironmentId(environment);
        RuntimeEnvironmentTransactionState state = GetState(active);
        if (state.ContainsEnvironment(environmentId))
        {
            return;
        }

        RuntimeEnvironmentParent? parent = null;
        if (environment is InheritedRuntimeEnvironment inheritedEnvironment)
        {
            EnsureEnvironmentExists(inheritedEnvironment.Parent, active);
            parent = CaptureParentView(inheritedEnvironment.Parent);
        }
        RuntimeEnvironmentNode node = new(environment.Name, environment.IsTransient, parent);
        state.AddEnvironment(environmentId, node, environment is EnvironmentLike source ? source.SnapshotStoredBindings() : environment);
    }

    public static RuntimeEnvironmentParent CaptureParentView(IRuntimeEnvironment environment) =>
        new(GetEnvironmentId(environment), environment.Namespace, environment.OverrideResolutionTags);

    public RuntimeEnvironmentTransactionState GetState(ActiveTransaction active) =>
        active.Current.GetParticipantState(participant);

    private static RuntimeEnvironmentId GetEnvironmentId(IRuntimeEnvironment environment)
    {
        if (environment is not ITransactionalRuntimeEnvironment transactionalEnvironment)
        {
            throw new InvalidOperationException($"Runtime environment type '{environment.GetType().FullName}' does not expose transactional environment identity.");
        }
        return transactionalEnvironment.EnvironmentId;
    }
}
