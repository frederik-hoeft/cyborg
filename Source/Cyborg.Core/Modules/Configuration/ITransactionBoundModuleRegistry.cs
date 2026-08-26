using Cyborg.Core.Modules.Runtime.Transactions;

namespace Cyborg.Core.Modules.Configuration;

internal interface ITransactionBoundModuleRegistry
{
    void Bind(RuntimeModuleRegistryTransactionState state);
}
