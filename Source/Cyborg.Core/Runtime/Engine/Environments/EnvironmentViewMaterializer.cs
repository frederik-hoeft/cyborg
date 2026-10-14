using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine.Environments;

/// <summary>
/// Constructs transaction-bound environment views from graph topology and recorded parent-view bindings.
/// It never consults the module invocation hierarchy to reconstruct structural parents.
/// </summary>
internal sealed class EnvironmentViewMaterializer(IRuntimeEnvironmentFactory factory, RuntimeEnvironmentGraphAccess graph)
{
    public IRuntimeEnvironment BindTransaction(IRuntimeEnvironment environment, ActiveTransaction active)
    {
        graph.EnsureEnvironmentExists(environment, active);
        return factory.BindTransaction(environment, graph.Participant, active);
    }

    public IRuntimeEnvironment Materialize(RuntimeEnvironmentId environmentId, string ns, IReadOnlyCollection<string> overrideResolutionTags, ActiveTransaction active)
    {
        RuntimeEnvironmentTransactionState state = graph.GetState(active);
        IRuntimeEnvironment environment = MaterializeCore(environmentId, ns, active, state, visited: []);
        return overrideResolutionTags.Count == 0
            ? environment
            : environment.WithOverrideResolutionTags(overrideResolutionTags);
    }

    private IRuntimeEnvironment MaterializeCore(
        RuntimeEnvironmentId environmentId,
        string ns,
        ActiveTransaction active,
        RuntimeEnvironmentTransactionState state,
        HashSet<RuntimeEnvironmentId> visited)
    {
        if (!visited.Add(environmentId))
        {
            throw new InvalidOperationException("Runtime environment topology contains an inheritance cycle.");
        }

        RuntimeEnvironmentNode node = graph.GetNode(environmentId, state);
        IRuntimeEnvironment? parent = null;
        if (node.Parent is RuntimeEnvironmentParent parentView)
        {
            parent = MaterializeCore(parentView.EnvironmentId, parentView.Namespace, active, state, visited);
            if (parentView.OverrideResolutionTags.Count > 0)
            {
                parent = parent.WithOverrideResolutionTags(parentView.OverrideResolutionTags);
            }
        }
        IRuntimeEnvironment environment = factory.CreateTransactionView(environmentId, node, parent, ns, graph.Participant, active);
        visited.Remove(environmentId);
        return environment;
    }
}
