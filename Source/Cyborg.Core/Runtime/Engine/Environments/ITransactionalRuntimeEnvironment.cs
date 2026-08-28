using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine.Environments;

internal interface ITransactionalRuntimeEnvironment
{
    RuntimeEnvironmentId EnvironmentId { get; }

    IRuntimeEnvironment BindTransaction(
        RuntimeEnvironmentTransactionParticipant participant,
        ExecutionTransaction transaction);
}
