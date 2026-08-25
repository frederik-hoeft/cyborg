using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

public sealed class RootModuleRuntime : ModuleRuntimeBase
{
    private protected override IModuleRuntime Root => this;

    private protected override IModuleRuntime? Parent => null;

    public RootModuleRuntime(
        GlobalRuntimeEnvironment defaultEnvironment,
        ILoggerFactory loggerFactory,
        IServiceProvider? serviceProvider = null)
        : this(CreateState(defaultEnvironment, loggerFactory), loggerFactory, serviceProvider)
    {
    }

    private RootModuleRuntime(RootRuntimeState state, ILoggerFactory loggerFactory, IServiceProvider? serviceProvider)
        : base(state.EnvironmentContext, loggerFactory, state.Transaction, serviceProvider)
    {
    }

    private static RootRuntimeState CreateState(GlobalRuntimeEnvironment defaultEnvironment, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultEnvironment);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        RuntimeEnvironmentTransactionParticipant environments = new();
        TransactionCoordinator coordinator = new([environments]);
        RuntimeEnvironmentNode globalNode = new(
            defaultEnvironment.Name,
            defaultEnvironment.IsTransient,
            Parent: null,
            defaultEnvironment.TaggedStringConversionObserver);
        RuntimeEnvironmentSeed globalSeed = new(
            defaultEnvironment.EnvironmentId,
            globalNode,
            [.. defaultEnvironment],
            RegisterName: false);
        RuntimeEnvironmentTransactionSeed environmentSeed = new(
            defaultEnvironment.EnvironmentId,
            [globalSeed]);
        TransactionRootSeed seed = new TransactionRootSeed().With(environments, environmentSeed);
        ExecutionTransaction transaction = coordinator.CreateRoot(seed);
        RuntimeEnvironmentContext environmentContext = RuntimeEnvironmentContext.CreateRoot(
            defaultEnvironment,
            environments,
            transaction,
            loggerFactory);
        return new RootRuntimeState(transaction, environmentContext);
    }

    private sealed record RootRuntimeState(
        ExecutionTransaction Transaction,
        RuntimeEnvironmentContext EnvironmentContext);
}
