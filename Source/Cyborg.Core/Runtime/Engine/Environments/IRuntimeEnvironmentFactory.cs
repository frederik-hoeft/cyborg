using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine.Environments;

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
