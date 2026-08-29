using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

internal interface IRuntimeModuleRegistry
{
    ITransactionParticipant Participant { get; }

    void ApplySeed(ExecutionTransaction transaction, ModuleRegistrySeed seed);

    void BindExecutionScope(IServiceProvider services, ExecutionTransaction transaction);
}
