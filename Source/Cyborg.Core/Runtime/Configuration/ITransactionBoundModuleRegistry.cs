using Cyborg.Core.Runtime.Engine.Transactions;

namespace Cyborg.Core.Runtime.Configuration;

internal interface ITransactionBoundModuleRegistry
{
    void Bind(RuntimeModuleRegistryTransactionState state);
}
