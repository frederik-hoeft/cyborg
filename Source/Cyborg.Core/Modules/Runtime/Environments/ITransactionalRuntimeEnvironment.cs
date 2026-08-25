using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal interface ITransactionalRuntimeEnvironment
{
    RuntimeEnvironmentId EnvironmentId { get; }

    IRuntimeEnvironment BindTransaction(
        RuntimeEnvironmentTransactionParticipant participant,
        ExecutionTransaction transaction);
}
