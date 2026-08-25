using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal interface ITransactionalRuntimeEnvironment
{
    IRuntimeEnvironment BindTransaction(
        EnvironmentVariableTransactionParticipant participant,
        ExecutionTransaction transaction);
}
