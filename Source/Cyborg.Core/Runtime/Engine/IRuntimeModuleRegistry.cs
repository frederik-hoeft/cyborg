using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine;

internal interface IRuntimeModuleRegistry
{
    ITransactionParticipant Participant { get; }

    void ApplySeed(ModuleTransaction transaction, ModuleRegistrySeed seed);

    void BindExecutionScope(IServiceProvider services, ModuleTransaction transaction);
}
