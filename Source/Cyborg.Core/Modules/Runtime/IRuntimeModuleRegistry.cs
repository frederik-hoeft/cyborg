using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Transactions.Core;

namespace Cyborg.Core.Modules.Runtime;

internal interface IRuntimeModuleRegistry
{
    ITransactionParticipant Participant { get; }

    void ApplySeed(ExecutionTransaction transaction, ModuleRegistrySeed seed);

    void BindExecutionScope(IServiceProvider services, ExecutionTransaction transaction);
}
