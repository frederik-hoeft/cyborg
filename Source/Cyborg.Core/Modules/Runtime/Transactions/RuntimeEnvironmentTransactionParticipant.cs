using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions.Collections;
using Cyborg.Core.Modules.Runtime.Transactions.Core;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class RuntimeEnvironmentTransactionParticipant : ITransactionParticipant<RuntimeEnvironmentTransactionState>
{
    public RuntimeEnvironmentTransactionState CreateRootState(TransactionRootSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (!seed.TryGet(this, out RuntimeEnvironmentTransactionSeed environmentSeed))
        {
            throw new InvalidOperationException("The runtime environment transaction participant requires an environment root seed.");
        }

        List<KeyValuePair<RuntimeEnvironmentId, RuntimeEnvironmentNode>> nodes = [];
        List<KeyValuePair<string, RuntimeEnvironmentId>> registrations = [];
        List<KeyValuePair<EnvironmentVariableBinding, object?>> bindings = [];
        foreach (RuntimeEnvironmentSeed environment in environmentSeed.Environments)
        {
            nodes.Add(new KeyValuePair<RuntimeEnvironmentId, RuntimeEnvironmentNode>(environment.EnvironmentId, environment.Node));
            if (environment.RegisterName)
            {
                registrations.Add(new KeyValuePair<string, RuntimeEnvironmentId>(environment.Node.Name, environment.EnvironmentId));
            }
            foreach ((string name, object? value) in environment.Values)
            {
                bindings.Add(new KeyValuePair<EnvironmentVariableBinding, object?>(
                    new EnvironmentVariableBinding(environment.EnvironmentId, name),
                    value));
            }
        }

        if (!nodes.Any(node => node.Key == environmentSeed.GlobalEnvironmentId))
        {
            throw new InvalidOperationException("The environment root seed does not contain its logical global environment.");
        }

        RuntimeEnvironmentGraphState graph = new(
            new TransactionalDictionary<RuntimeEnvironmentId, RuntimeEnvironmentNode>(nodes),
            new TransactionalDictionary<string, RuntimeEnvironmentId>(registrations, StringComparer.Ordinal));
        RuntimeEnvironmentBindingState bindingState = new(
            new TransactionalDictionary<EnvironmentVariableBinding, object?>(bindings));
        return new RuntimeEnvironmentTransactionState(environmentSeed.GlobalEnvironmentId, graph, bindingState);
    }
}
