using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Transactions.Collections;
using Cyborg.Core.Modules.Runtime.Transactions.Core;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal sealed class RuntimeModuleRegistryTransactionParticipant : ITransactionParticipant<RuntimeModuleRegistryTransactionState>
{
    public RuntimeModuleRegistryTransactionState CreateRootState(TransactionRootSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (!seed.TryGet(this, out ModuleRegistrySeed moduleSeed))
        {
            return new RuntimeModuleRegistryTransactionState(
                new TransactionalDictionary<string, ModuleContext>(StringComparer.Ordinal));
        }
        return RuntimeModuleRegistryTransactionState.Create(moduleSeed);
    }
}
