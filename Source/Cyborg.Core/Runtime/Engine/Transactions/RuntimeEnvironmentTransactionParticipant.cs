using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Collections;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine.Transactions;

internal sealed class RuntimeEnvironmentTransactionParticipant : ITransactionParticipant<RuntimeEnvironmentTransactionState>
{
    public RuntimeEnvironmentTransactionState CreateRootState(TransactionRootSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (!seed.TryGet(this, out RuntimeEnvironmentTransactionSeed? environmentSeed))
        {
            throw new InvalidOperationException("The runtime environment transaction participant requires an environment root seed.");
        }

        List<KeyValuePair<RuntimeEnvironmentId, RuntimeEnvironmentNode>> nodes = [];
        List<KeyValuePair<string, RuntimeEnvironmentId>> registrations = [];
        List<KeyValuePair<EnvironmentVariableBinding, object?>> bindings = [];
        foreach (RuntimeEnvironmentSeed environment in environmentSeed.Environments)
        {
            nodes.Add(KeyValuePair.Create(environment.EnvironmentId, environment.Node));
            if (environment.RegisterName)
            {
                registrations.Add(KeyValuePair.Create(environment.Node.Name, environment.EnvironmentId));
            }
            foreach ((string name, object? value) in environment.Values)
            {
                bindings.Add(KeyValuePair.Create(new EnvironmentVariableBinding(environment.EnvironmentId, name), value));
            }
        }

        if (!nodes.Any(node => node.Key == environmentSeed.GlobalEnvironmentId))
        {
            throw new InvalidOperationException("The environment root seed does not contain its logical global environment.");
        }

        RuntimeEnvironmentGraphState graph = new(nodes.ToTransactionalDictionary(), registrations.ToTransactionalDictionary(StringComparer.Ordinal));
        RuntimeEnvironmentBindingState bindingState = new(bindings.ToTransactionalDictionary());
        return new RuntimeEnvironmentTransactionState(environmentSeed.GlobalEnvironmentId, graph, bindingState);
    }
}
