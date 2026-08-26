using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal interface IRuntimeEnvironmentFactory
{
    GlobalRuntimeEnvironment CreateGlobalEnvironment();

    IEnvironmentLike CreateEnvironmentLike(string ns);

    IRuntimeEnvironment BindTransaction(
        IRuntimeEnvironment environment,
        RuntimeEnvironmentTransactionParticipant participant,
        ExecutionTransaction transaction);

    IRuntimeEnvironment CreateTransactionView(
        RuntimeEnvironmentId environmentId,
        RuntimeEnvironmentNode node,
        IRuntimeEnvironment? parent,
        string ns,
        RuntimeEnvironmentTransactionParticipant participant,
        ExecutionTransaction transaction);
}
