using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Transactions.Collections;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine.Transactions;

internal sealed class RuntimeModuleRegistryTransactionParticipant : ITransactionParticipant<RuntimeModuleRegistryTransactionState>
{
    public RuntimeModuleRegistryTransactionState CreateRootState(TransactionRootSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (!seed.TryGet(this, out ModuleRegistrySeed? moduleSeed))
        {
            return new RuntimeModuleRegistryTransactionState(new TransactionalDictionary<string, ModuleContext>(StringComparer.Ordinal));
        }
        return RuntimeModuleRegistryTransactionState.Create(moduleSeed);
    }
}
